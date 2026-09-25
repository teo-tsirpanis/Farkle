// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using BitCollections;

namespace Farkle.Builder.Lr;

partial struct LrBuild
{
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
                followKernelItems[gotoIndex] = followKernelItems[gotoIndex].Set(i, true);
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
}
