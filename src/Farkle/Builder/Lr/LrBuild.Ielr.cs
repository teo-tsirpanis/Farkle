// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using BitCollections;
using Farkle.Collections;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

partial struct LrBuild
{
    /// <summary>
    /// Wraps <see cref="LrConflictResolver.HasPrecedenceInfo"/> to work with LR builder types.
    /// </summary>
    private bool HasPrecedenceInfo(Symbol symbol, LrConflictContribution contribution)
    {
        Debug.Assert(symbol.IsTerminal);
        if (ConflictResolver is null)
        {
            return false;
        }
        if (contribution.IsAccept)
        {
            return false;
        }
        return ConflictResolver.HasPrecedenceInfo(contribution.IsReduce(out Production production) ? TranslateProduction(production) : TranslateTerminal(symbol));
    }

    /// <summary>
    /// Resolves a conflict between two contributions when encountering the given symbol.
    /// </summary>
    /// <remarks>
    /// This method is intended to be used inside the IELR algorithm. For the final conflict
    /// resolution at the end of building a grammar, use <see cref="ConflictResolvingLrStateMachine"/>.
    /// </remarks>
    private LrConflictResolverDecision ResolveConflict(Symbol conflictSymbol, LrConflictContribution contribution1, LrConflictContribution contribution2)
    {
        Debug.Assert(conflictSymbol.IsTerminal);
        if (ConflictResolver is null)
        {
            return LrConflictResolverDecision.CannotChoose;
        }
        if (contribution1.IsAccept || contribution2.IsAccept)
        {
            Debug.Assert(!(contribution1.IsAccept && contribution2.IsAccept), "Accept/Accept conflict is not possible");
            // Accept/Reduce conflicts cannot be resolved.
            return LrConflictResolverDecision.CannotChoose;
        }
        if (conflictSymbol.Index == EndSymbolIndex)
        {
            return ConflictResolver.ResolveEndOfFileConflict(TranslateEndOfFileConflictContribution(contribution1), TranslateEndOfFileConflictContribution(contribution2));
        }
        return ConflictResolver.ResolveConflict(TranslateTerminal(conflictSymbol),
            TranslateConflictContribution(contribution1), TranslateConflictContribution(contribution2));
    }

    private ImmutableArray<BitArrayNeo> ComputePredecessors(Lr0StateMachine stateMachine)
    {
        int stateCount = stateMachine.States.Length;
        ReadOnlySpan<GotoInfo> gotos = stateMachine.Gotos.AsSpan();

        var predecessors = ImmutableArray.CreateBuilder<BitArrayNeo>(stateCount);
        for (int i = 0; i < stateCount; i++)
        {
            predecessors.Add(new BitArrayNeo(stateCount));
        }

        for (int i = 0; i < stateCount; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            var state = stateMachine.States[i];
            foreach (var x in state.Transitions.Keys)
            {
                int destination = state.FollowTransition(x, gotos);
                predecessors[destination][i] = true;
            }
        }

        return predecessors.MoveToImmutable();
    }

    // Use BitSet because it's allocation-free on small sets, and the assumption is that
    // each state has "few" kernel items.
    private ImmutableArray<BitSet> ComputeGotoFollowKernelItems(Lr0StateMachine stateMachine,
        ImmutableArray<int> productionNullableStarts, ImmutableArray<GotoFollowDependency> dependencies)
    {
        var gotos = stateMachine.Gotos;
        var followKernelItems = ImmutableArray.CreateBuilder<BitSet>(gotos.Length);
        for (int i = 0; i < gotos.Length; i++)
        {
            followKernelItems.Add(BitSet.Empty);
        }

        // Initialize goto follow kernel items:
        // For each kernel item whose production members after the dot are all nullable nonterminals,
        // its goto follow kernel items set starts with the kernel item itself.
        foreach (var state in stateMachine.States)
        {
            CancellationToken.ThrowIfCancellationRequested();
            for (int i = 0; i < state.KernelItems.Count; i++)
            {
                Lr0Item item = state.KernelItems[i];
                var productionMembers = Syntax.GetProductionMembers(item.Production);
                // Skip items whose dot is at the end (they won't have a transition),
                // whose production is the start production (because of the implicit
                // end symbol), whose symbol at the dot is not a nonterminal (or we
                // wouldn't have a GOTO), or whose symbols after the dot are not all
                // nullable.
                if (item.DotPosition == productionMembers.Count
                    || item.Production.Equals(Syntax.StartProduction)
                    || productionMembers[item.DotPosition] is not { IsTerminal: false } symbolAtDot
                    || item.DotPosition + 1 < productionNullableStarts[item.Production.Index])
                {
                    continue;
                }
                int gotoIndex = state.Transitions[symbolAtDot];
                followKernelItems[gotoIndex] = BitSet.Singleton(i);
            }
        }

        // Propagate goto follow kernel items with internal dependencies.
        bool changed;
        do
        {
            CancellationToken.ThrowIfCancellationRequested();

            changed = false;
            foreach (var dependency in dependencies)
            {
                if (dependency.DependencyKind != GotoFollowDependencyKinds.Internal)
                {
                    continue;
                }
                var source = followKernelItems[dependency.FromGoto];
                var destination = followKernelItems[dependency.ToGoto];
                var newSource = BitSet.Union(source, destination);
                if (!source.Equals(newSource))
                {
                    followKernelItems[dependency.FromGoto] = newSource;
                    changed = true;
                }
            }
        } while (changed);

        return followKernelItems.MoveToImmutable();
    }

    private ImmutableArray<ConflictDescription> ComputeConflicts(Lr0StateMachine stateMachine,
        GroupedIndexedList<ReductionLookahead> reductionLookaheads)
    {
        var conflicts = ImmutableArray.CreateBuilder<ConflictDescription>();
        var seenTerminals = new TerminalSet(Syntax);
        var conflictingTerminals = new TerminalSet(Syntax);

        // Traverse all actions of each state to find terminals with conflicting actions,
        // and then traverse the conflicting terminals to collect the contributions of each conflict.
        for (int i = 0; i < stateMachine.States.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();

            seenTerminals.SetAll(false);
            conflictingTerminals.SetAll(false);

            var state = stateMachine.States[i];
            var reductions = reductionLookaheads.GetItemsWithKey(i);
            // A state with no reduce actions cannot have conflicts.
            if (reductions is [])
            {
                continue;
            }

            foreach (var s in state.Transitions.Keys)
            {
                if (!s.IsTerminal)
                {
                    continue;
                }
                // Shift actions are unique per terminal, so we don't need to check for duplicates.
                seenTerminals[s] = true;
            }
            foreach (var r in reductions)
            {
                foreach (var s in r.Lookahead)
                {
                    if (seenTerminals.Set(s, true))
                    {
                        continue;
                    }
                    conflictingTerminals[s] = true;
                }
            }

            foreach (var t in conflictingTerminals)
            {
                // Conflicts are usually between two contributions.
                var contributions = ImmutableArray.CreateBuilder<LrConflictContribution>(2);

                if (state.Transitions.TryGetValue(t, out int shiftState))
                {
                    contributions.Add(LrConflictContribution.CreateShift(shiftState, Syntax));
                }
                foreach (var r in reductions)
                {
                    if (r.Lookahead[t])
                    {
                        contributions.Add(LrConflictContribution.CreateReduce(r.Production, Syntax));
                    }
                }

                conflicts.Add(new(i, t, contributions.DrainToImmutable()));
            }
        }
        return conflicts.DrainToImmutable();
    }

    private readonly struct ConflictDescription(int stateIndex, Symbol symbol, ImmutableArray<LrConflictContribution> contributions)
    {
        public int StateIndex { get; } = stateIndex;

        public Symbol Symbol { get; } = symbol;

        public ImmutableArray<LrConflictContribution> Contributions { get; } = contributions;
    }
}
