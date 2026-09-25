// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Collections;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

internal partial struct LrBuild
{
    private bool HasPrecedenceInfo(Symbol symbol, LrConflictContribution contribution) => GetPrecedence(symbol, contribution) >= 0;

    private int GetPrecedence(Symbol symbol, LrConflictContribution contribution)
    {
        Debug.Assert(symbol.IsTerminal);
        if (OperatorInfoProvider is null)
        {
            return -1;
        }
        if (contribution.IsAccept)
        {
            return -1;
        }
        return OperatorInfoProvider.GetPrecedence(contribution.IsReduce(out Production production) ? TranslateProduction(production) : TranslateTerminal(symbol));
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
                    contributions.Add(LrConflictContribution.CreateShift(shiftState));
                }
                foreach (var r in reductions)
                {
                    if (r.Lookahead[t])
                    {
                        contributions.Add(LrConflictContribution.CreateReduce(r.Production));
                    }
                }

                conflicts.Add(new(i, t, contributions.DrainToImmutable()));
            }
        }
        return conflicts.DrainToImmutable();
    }

    private readonly struct ConflictDescription
    {
        public int StateIndex { get; }

        public Symbol Symbol { get; }

        public ImmutableArray<LrConflictContribution> Contributions { get; }

        public ConflictDescription(int stateIndex, Symbol symbol, ImmutableArray<LrConflictContribution> contributions)
        {
            Debug.Assert(contributions.Length >= 2);
            Debug.Assert(contributions.Skip(1).All(x => x.IsReduce(out _)));
            StateIndex = stateIndex;
            Symbol = symbol;
            Contributions = contributions;
        }
    }
}
