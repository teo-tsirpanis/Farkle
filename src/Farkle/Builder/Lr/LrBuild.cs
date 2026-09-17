// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Collections;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars.Writers;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic for building an LR(1) state machine from a set of
/// syntax rules.
/// </summary>
internal readonly partial struct LrBuild
{
    private readonly AugmentedSyntaxProvider Syntax;

    private readonly OperatorInfoProvider? OperatorInfoProvider;

    private readonly CancellationToken CancellationToken;

    private readonly BuilderLogger Log;

    private LrBuild(IGrammarSyntaxProvider syntax, OperatorInfoProvider? operatorInfoProvider, BuilderLogger log,
        CancellationToken cancellationToken)
    {
        Syntax = new(syntax);
        OperatorInfoProvider = operatorInfoProvider;
        CancellationToken = cancellationToken;
        Log = log;
    }

    /// <summary>
    /// Builds an LR(1) state machine that can parse the syntax of a grammar.
    /// </summary>
    /// <param name="syntax">The syntax of the grammar.</param>
    /// <param name="algorithm">The algorithm to use (LALR or IELR).</param>
    /// <param name="operatorInfoProvider">The operator info provider to use. Optional.</param>
    /// <param name="log">Used to log events in the building process.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static LrWriter Build(IGrammarSyntaxProvider syntax, ParserGenerationAlgorithm algorithm,
        OperatorInfoProvider? operatorInfoProvider = null, BuilderLogger log = default, CancellationToken cancellationToken = default) =>
        new LrBuild(syntax, operatorInfoProvider, log, cancellationToken).Build(algorithm);

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

        if (algorithm == ParserGenerationAlgorithm.Lalr1
            || (ComputeConflicts(lr0StateMachine, reductionLookaheads) is var conflicts && conflicts is []))
        {
            return WriteStateMachine(lr0StateMachine, reductionLookaheads);
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
                return WriteStateMachine(newLr0StateMachine, newReductionLookaheads);
            }
            else
            {
                // No need to recompute reduction lookaheads if we didn't split any states.
                return WriteStateMachine(lr0StateMachine, reductionLookaheads);
            }
        }

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

    private LrWriter WriteStateMachine(Lr0StateMachine lr0StateMachine, GroupedIndexedList<ReductionLookahead> reductionLookaheads)
    {
        var writer = new LrWriter(lr0StateMachine.States.Length);
        if (OperatorInfoProvider is null)
        {
            // Fast path if there's no operator info provider; we cannot resolve any conflicts if they arise.
            for (int i = 0; i < lr0StateMachine.States.Length; i++)
            {
                CancellationToken.ThrowIfCancellationRequested();
                Write_NoConflicts(in lr0StateMachine.States.ItemRef(i), reductionLookaheads.GetItemsWithKey(i));
            }
        }
        else
        {
            var infoProvider = OperatorInfoProvider;
            var conflictResolvers = new Dictionary<Symbol, LrConflictResolver>();
            var precedences = new List<int>();
            var symbolsWithIncompletePrecedenceInfo = new TerminalSet(Syntax);
            for (int i = 0; i < lr0StateMachine.States.Length; i++)
            {
                CancellationToken.ThrowIfCancellationRequested();
                ref readonly var state = ref lr0StateMachine.States.ItemRef(i);
                var reductions = reductionLookaheads.GetItemsWithKey(i);
                // A state with no reduce actions cannot have conflicts.
                if (reductions.IsEmpty)
                {
                    Write_NoConflicts(in lr0StateMachine.States.ItemRef(i), reductions);
                    continue;
                }

                // Perform two passes over all states. One to record conflicts, and another to see which contributions
                // belong in the dominant set.
                {
                    foreach ((Symbol symbol, int value) in state.Transitions)
                    {
                        if (symbol.IsTerminal)
                        {
                            AddConflict(symbol, LrConflictContribution.CreateShift(value));
                        }
                    }
                    foreach (ref readonly var reduction in reductions)
                    {
                        foreach (var terminal in reduction.Lookahead)
                        {
                            AddConflict(terminal, LrConflictContribution.CreateReduce(reduction.Production));
                        }
                    }
                }

                CancellationToken.ThrowIfCancellationRequested();

                {
                    int j = 0;
                    foreach ((Symbol symbol, int value) in state.Transitions)
                    {
                        if (symbol.IsTerminal)
                        {
                            if (ContainsInDominantSet(symbol, LrConflictContribution.CreateShift(value), precedences[j++]))
                            {
                                writer.AddShift(TranslateTerminal(symbol), value);
                            }
                        }
                        else
                        {
                            writer.AddGoto(TranslateNonterminal(symbol), lr0StateMachine.Gotos[value].ToState);
                        }
                    }
                    foreach (ref readonly var reduction in reductions)
                    {
                        foreach (var terminal in reduction.Lookahead)
                        {
                            if (ContainsInDominantSet(terminal, LrConflictContribution.CreateReduce(reduction.Production), precedences[j++]))
                            {
                                AddReduce(writer, terminal, reduction.Production);
                            }
                        }
                    }
                    Debug.Assert(j == precedences.Count);
                    writer.FinishState();
                }

                conflictResolvers.Clear();
                precedences.Clear();
                symbolsWithIncompletePrecedenceInfo.SetAll(false);

                void AddConflict(Symbol symbol, LrConflictContribution contribution)
                {
                    ref var resolver = ref CollectionsMarshal.GetValueRefOrAddDefault(conflictResolvers, symbol, out bool exists);
                    if (!exists)
                    {
                        resolver = new LrConflictResolver(infoProvider);
                    }
                    var decision = resolver.Add(symbol, contribution, out int precedence);
                    precedences.Add(precedence);
                    if (decision == LrConflictResolverDecision.NoPrecedence)
                    {
                        symbolsWithIncompletePrecedenceInfo[symbol] = true;
                    }
                }

                bool ContainsInDominantSet(Symbol symbol, LrConflictContribution contribution, int precedence)
                {
                    if (symbolsWithIncompletePrecedenceInfo[symbol])
                    {
                        return true;
                    }
                    ref var resolver = ref CollectionsMarshal.GetValueRefOrNullRef(conflictResolvers, symbol);
                    Debug.Assert(!Unsafe.IsNullRef(ref resolver));
                    return resolver.ContainsInDominantSet(contribution, precedence);
                }
            }
        }
        return writer;

        void Write_NoConflicts(in Lr0State state, ReadOnlySpan<ReductionLookahead> reductionLookaheads)
        {
            foreach ((Symbol symbol, int value) in state.Transitions)
            {
                if (symbol.IsTerminal)
                {
                    writer.AddShift(TranslateTerminal(symbol), value);
                }
                else
                {
                    writer.AddGoto(TranslateNonterminal(symbol), lr0StateMachine.Gotos[value].ToState);
                }
            }
            foreach (ref readonly var reductionLookahead in reductionLookaheads)
            {
                foreach (var terminal in reductionLookahead.Lookahead)
                {
                    AddReduce(writer, terminal, reductionLookahead.Production);
                }
            }
            writer.FinishState();
        }
    }

    private static void AddReduce(LrWriter writer, Symbol terminal, Production production)
    {
        if (production.Index == StartProductionIndex)
        {
            Debug.Assert(terminal.Index == EndSymbolIndex);
            writer.AddEofAccept();
            return;
        }
        if (terminal.Index == EndSymbolIndex)
        {
            writer.AddEofReduce(TranslateProduction(production));
            return;
        }
        writer.AddReduce(TranslateTerminal(terminal), TranslateProduction(production));
    }
}
