// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using BitCollections;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

partial struct LrBuild
{
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
        return ConflictResolver.ResolveConflict(TranslateTerminalIndex(conflictSymbol.Index),
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
                // or whose symbols after the dot are not all nullable.
                if (item.DotPosition == productionMembers.Count
                    || productionNullableStarts[item.Production.Index] < item.DotPosition)
                {
                    continue;
                }
                var symbolAtDot = productionMembers[item.DotPosition];
                // Due to productionNullableStarts check above, the symbol at the dot must be a nonterminal.
                Debug.Assert(!symbolAtDot.IsTerminal);
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
        ImmutableArray<Dictionary<Production, TerminalSet>?> reductionLookaheads)
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
            // A state with no reduce actions cannot have conflicts.
            if (reductionLookaheads[i] is not { } lookaheads)
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
            foreach (var x in lookaheads.Values)
            {
                foreach (var s in x)
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
                foreach (var x in lookaheads)
                {
                    if (x.Value[t])
                    {
                        contributions.Add(LrConflictContribution.CreateReduce(x.Key, Syntax));
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
