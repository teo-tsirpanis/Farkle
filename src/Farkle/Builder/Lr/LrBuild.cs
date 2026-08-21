// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars.Writers;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic for building an LR(1) state machine from a set of
/// syntax rules.
/// </summary>
internal readonly partial struct LrBuild
{
    private readonly AugmentedSyntaxProvider Syntax;

    private readonly LrConflictResolver? ConflictResolver;

    private readonly CancellationToken CancellationToken;

    private readonly BuilderLogger Log;

    private LrBuild(IGrammarSyntaxProvider syntax, LrConflictResolver? conflictResolver, BuilderLogger log,
        CancellationToken cancellationToken)
    {
        Syntax = new(syntax);
        ConflictResolver = conflictResolver;
        CancellationToken = cancellationToken;
        Log = log;
    }

    /// <summary>
    /// Builds an LR(1) state machine that can parse the syntax of a grammar.
    /// </summary>
    /// <param name="syntax">The syntax of the grammar.</param>
    /// <param name="algorithm">The algorithm to use (LALR or IELR).</param>
    /// <param name="conflictResolver">The conflict resolver to use. Optional.</param>
    /// <param name="log">Used to log events in the building process.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static LrWriter Build(IGrammarSyntaxProvider syntax, ParserGenerationAlgorithm algorithm,
        LrConflictResolver? conflictResolver = null, BuilderLogger log = default, CancellationToken cancellationToken = default) =>
        new LrBuild(syntax, conflictResolver, log, cancellationToken).Build(algorithm);

    private LrWriter Build(ParserGenerationAlgorithm algorithm)
    {
        var lr0StateMachine = ComputeLr0StateMachine();
        var nullableNonterminals = ComputeNullableNonterminals();
        var productionNullableStarts = ComputeProductionNullableStarts(nullableNonterminals);
        var gotoFollowDependencies = ComputeGotoFollowDependencies(lr0StateMachine, nullableNonterminals, productionNullableStarts);
        var gotoFollows = ComputeInitialGotoFollows(lr0StateMachine);
        // The rule is, after taking a successor dependency, no internal dependency can be followed.
        // We can propagate all successor dependencies first, but can also propagate internal dependencies
        // at the same time. This has an equivalent effect according to §3.3.3 of the IELR paper.
        PropagateGotoFollows(gotoFollowDependencies, GotoFollowDependencyKinds.Successor | GotoFollowDependencyKinds.Internal, gotoFollows);
        // IELR needs the always follows.
        var alwaysFollows = algorithm == ParserGenerationAlgorithm.Ielr1 ? Clone(gotoFollows) : default;
        PropagateGotoFollows(gotoFollowDependencies, GotoFollowDependencyKinds.Internal | GotoFollowDependencyKinds.Predecessor, gotoFollows);
        var reductionLookaheads = ComputeReductionLookaheads(lr0StateMachine, gotoFollows);

        LrStateMachine stateMachine;
        if (algorithm == ParserGenerationAlgorithm.Lalr1
            || (ComputeConflicts(lr0StateMachine, reductionLookaheads) is var conflicts && conflicts is []))
        {
            stateMachine = new DefaultLrStateMachine(lr0StateMachine, reductionLookaheads);
        }
        else
        {
            Log.InformationLocalized(nameof(Resources.Builder_FoundConflictsSwitchingToIelr), conflicts.Length);

            // IELR Phase 1: Compute Auxiliary Tables (always_follows has been computed earlier)
            var predecessors = ComputePredecessors(lr0StateMachine);
            var followKernelItems = ComputeGotoFollowKernelItems(lr0StateMachine, productionNullableStarts, gotoFollowDependencies);
            // IELR Phase 2: Compute Annotations
            var annotations = ComputeAnnotations(lr0StateMachine, conflicts, gotoFollows, alwaysFollows, predecessors, followKernelItems);
            // IELR Phase 3: Split States
            var newLr0StateMachine = SplitStates(lr0StateMachine, annotations, conflicts, alwaysFollows, followKernelItems);
            if (newLr0StateMachine.States.Length > lr0StateMachine.States.Length)
            {
                // IELR Phase 4: Compute Reduction Lookaheads
                var newGotoFollowDependencies = ComputeGotoFollowDependencies(newLr0StateMachine, nullableNonterminals, productionNullableStarts);
                var newGotoFollows = ComputeInitialGotoFollows(newLr0StateMachine);
                PropagateGotoFollows(newGotoFollowDependencies, GotoFollowDependencyKinds.Successor | GotoFollowDependencyKinds.Internal, newGotoFollows);
                PropagateGotoFollows(newGotoFollowDependencies, GotoFollowDependencyKinds.Internal | GotoFollowDependencyKinds.Predecessor, newGotoFollows);
                var newReductionLookaheads = ComputeReductionLookaheads(newLr0StateMachine, newGotoFollows);
                stateMachine = new DefaultLrStateMachine(newLr0StateMachine, newReductionLookaheads);
            }
            else
            {
                // No need to recompute reduction lookaheads if we didn't split any states.
                stateMachine = new DefaultLrStateMachine(lr0StateMachine, reductionLookaheads);
            }
        }

        if (ConflictResolver is not null)
        {
            stateMachine = new ConflictResolvingLrStateMachine(stateMachine, ConflictResolver);
        }
        return stateMachine.ToLrWriter();

        static ImmutableArray<TerminalSet> Clone(ImmutableArray<TerminalSet> array)
        {
            var builder = ImmutableArray.CreateBuilder<TerminalSet>(array.Length);
            foreach (var x in array)
            {
                builder.Add(new(x));
            }
            return builder.MoveToImmutable();
        }
    }
}
