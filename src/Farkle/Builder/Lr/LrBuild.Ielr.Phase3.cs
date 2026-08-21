// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using BitCollections;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

partial struct LrBuild
{
    private Lr0StateMachine SplitStates(Lr0StateMachine stateMachine, InadequacyAnnotationList annotationList,
        ImmutableArray<ConflictDescription> conflicts, ImmutableArray<TerminalSet> alwaysFollows,
        ImmutableArray<BitSet> followKernelItems)
    {
        if (annotationList.Count == 0)
        {
            // If there are no annotations, no lookaheads would be propagated and all isocores would be compatible,
            // so we can entirely skip this step.
            return stateMachine;
        }

        Log.Debug("Splitting LALR states");

        var syntax = Syntax;
        var newStates = ImmutableArray.CreateBuilder<Lr0State>();
        foreach (var state in stateMachine.States)
        {
            newStates.Add(state.Clone());
        }
        var newGotos = ImmutableArray.CreateBuilder<GotoInfo>();
        newGotos.AddRange(stateMachine.Gotos);

        // Lookahead sets are represented as TerminalSet[]?; one set per kernel item.
        // The array is null if all lookahead sets of a state are empty.
        TerminalSet[]?[] lookaheadFiltersCache = new TerminalSet[stateMachine.States.Length][];
        // Reusable lists for state compatibility tests.
        var stateContributions = new List<LrConflictContribution>();
        var candidateContributions = new List<LrConflictContribution>();

        var lalrIsocores = new List<int>();
        var isocoreNexts = new List<int>();
        var itemLookaheadSets = new List<TerminalSet[]?>();
        var lookaheadsRecomputed = new List<bool>();
        for (int i = 0; i < stateMachine.States.Length; i++)
        {
            lalrIsocores.Add(i);
            isocoreNexts.Add(i);
            itemLookaheadSets.Add(null);
            lookaheadsRecomputed.Add(false);
        }

        var stack = new Stack<(int FromState, int ToState, Symbol Symbol)>();
        for (int i = 0; i < newStates.Count; i++)
        {
            foreach (var transition in newStates[i].Transitions)
            {
                stack.Push((i, FollowTransition(transition), transition.Key));
                while (stack.TryPop(out var next))
                {
                    CancellationToken.ThrowIfCancellationRequested();

                    var lookaheads = PropagateLookaheads(next.FromState, next.ToState);

                    var found = false;
                    var compatibleState = next.ToState;
                    do
                    {
                        if (IsCompatible(in this, compatibleState, lookaheads))
                        {
                            found = true;
                            break;
                        }
                        compatibleState = isocoreNexts[compatibleState];
                    } while (compatibleState != next.ToState);
                    if (!found)
                    {
                        var newState = AddState(next.ToState, lookaheads);
                        UpdateTransition(next.FromState, next.Symbol, newState);
                    }
                    else if (!lookaheadsRecomputed[compatibleState])
                    {
                        Debug.Assert(lookaheads is null || newStates[compatibleState].KernelItems.Count == lookaheads.Length);
                        itemLookaheadSets[compatibleState] = lookaheads;
                        lookaheadsRecomputed[compatibleState] = true;
                    }
                    else
                    {
                        UpdateTransition(next.FromState, next.Symbol, compatibleState);
                        MergeLookaheads(compatibleState, lookaheads);
                    }
                }
            }
        }

        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Split {stateMachine.States.Length} LALR states into {newStates.Count} IELR states");
        }

        return new Lr0StateMachine(newStates.DrainToImmutable(), newGotos.DrainToImmutable());

        void MergeLookaheads(int state, TerminalSet[]? lookaheads)
        {
            if (lookaheads is null)
            {
                // Empty lookahead set, nothing to merge.
                return;
            }
            var merged = false;
            var stateLookaheads = itemLookaheadSets[state];
            if (stateLookaheads is null)
            {
                // No lookaheads have been present for this state, so we can just assign the new lookaheads.
                Debug.Assert(newStates[state].KernelItems.Count == lookaheads.Length);
                itemLookaheadSets[state] = lookaheads;
                merged = true;
            }
            else
            {
                Debug.Assert(stateLookaheads.Length == lookaheads.Length);
                for (int i = 0; i < stateLookaheads.Length; i++)
                {
                    merged |= stateLookaheads[i].Or(lookaheads[i]);
                }
            }
            if (merged)
            {
                foreach (var transition in newStates[state].Transitions)
                {
                    var toState = FollowTransition(transition);
                    if (lookaheadsRecomputed[toState])
                    {
                        stack.Push((state, toState, transition.Key));
                    }
                }
            }
        }

        bool IsCompatible(in LrBuild @this, int state, TerminalSet[]? candidateLookaheads)
        {
            if (!lookaheadsRecomputed[state])
            {
                return true;
            }
            var stateLookaheads = itemLookaheadSets[state];
            foreach (var annotation in annotationList.GetAnnotations(lalrIsocores[state]))
            {
                stateContributions.Clear();
                candidateContributions.Clear();
                if (!TryFillDominantContributions(@this, annotation, stateLookaheads, stateContributions)
                    || !TryFillDominantContributions(@this, annotation, candidateLookaheads, candidateContributions))
                {
                    continue;
                }
                stateContributions.Sort();
                candidateContributions.Sort();
                if (!stateContributions.SequenceEqual(candidateContributions))
                {
                    return false;
                }
            }
            return true;
        }

        bool TryFillDominantContributions(in LrBuild @this, InadequacyAnnotation annotation, TerminalSet[]? lookaheads, List<LrConflictContribution> result)
        {
            Debug.Assert(result.Count == 0);
            var conflict = conflicts[annotation.ConflictIndex];
            bool isChooseNeitherDominating = false;
            for (int i = 0; i < conflict.Contributions.Length; i++)
            {
                var candidateContribution = conflict.Contributions[i];
                var matrixRow = annotation.ContributionMatrix[i];
                if (!FilterContribution(matrixRow, conflict.Symbol, lookaheads))
                {
                    continue;
                }
                if (result.Count == 0)
                {
                    result.Add(candidateContribution);
                    continue;
                }
                // This works similarly to IsSplitStableDominantContribution from Phase 2, but we need to keep
                // track of which contributions are dominant.
                var decision = @this.ResolveConflict(conflict.Symbol, result[0], candidateContribution);
                switch (decision)
                {
                    case LrConflictResolverDecision.ChooseOption1:
                        break;
                    case LrConflictResolverDecision.ChooseOption2:
                        result.Clear();
                        result.Add(candidateContribution);
                        isChooseNeitherDominating = false;
                        break;
                    case LrConflictResolverDecision.CannotChoose:
                    case LrConflictResolverDecision.ChooseNeither:
                        isChooseNeitherDominating |= decision == LrConflictResolverDecision.ChooseNeither;
                        result.Add(candidateContribution);
                        break;
                }
            }

            // We need to distinguish between "no contributions passed the filter", and
            // "some contributions passed the filter", but we return none of them, because
            // the conflict resolver decided that none of them should be chosen.
            bool hasResult = result.Count != 0;
            if (isChooseNeitherDominating)
            {
                result.Clear();
            }
            return hasResult;

            static bool FilterContribution(BitSet? row, Symbol conflictSymbol, TerminalSet[]? lookaheads)
            {
                if (row is null)
                {
                    return true;
                }
                foreach (var column in row.Value)
                {
                    if (lookaheads?[column][conflictSymbol] ?? false)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        TerminalSet[]? PropagateLookaheads(int fromState, int toState)
        {
            var filters = GetLookaheadFilters(toState);
            if (filters is null)
            {
                // If the filters set is empty (e.g. the state has no annotations), then the lookahead
                // sets will also be empty, so we can skip the propagation.
                return null;
            }
            var kernelItems = newStates[toState].KernelItems;
            var result = NewLookaheadSet(kernelItems.Count);
            for (int i = 0; i < result.Length; i++)
            {
                var item = kernelItems[i];
                var resultRow = result[i];
                var filter = filters[i];

                // The only kernel item with the dot at the start is in the start state, which cannot occur in toState.
                Debug.Assert(item.DotPosition > 0);
                if (item.DotPosition == 1)
                {
                    var productionHead = syntax.GetProductionHead(item.Production.Index);
                    FillGotoFollowSet(fromState, productionHead, resultRow);
                }
                else if (itemLookaheadSets[fromState] is { } predecessorLookaheads)
                {
                    var previousItem = new Lr0Item(item.Production, item.DotPosition - 1);
                    int previousItemIndex = newStates[fromState].KernelItems.IndexOf(previousItem);
                    resultRow.Or(predecessorLookaheads[previousItemIndex]);
                }
                resultRow.And(filter);
            }
            return result;
        }

        void FillGotoFollowSet(int state, Symbol productionHead, TerminalSet result)
        {
            Debug.Assert(!productionHead.IsTerminal);
            if (!newStates[lalrIsocores[state]].Transitions.TryGetValue(productionHead, out int gotoIndex))
            {
                Debug.Assert(productionHead.Equals(syntax.StartSymbol));
                return;
            }

            result.Or(alwaysFollows[gotoIndex]);

            var lookaheadSet = itemLookaheadSets[state];
            foreach (var item in followKernelItems[gotoIndex])
            {
                // At this point, we should have computed the state's lookahead set.
                Debug.Assert(lookaheadSet is not null);
                result.Or(lookaheadSet[item]);
            }
        }

        TerminalSet[]? GetLookaheadFilters(int state)
        {
            state = lalrIsocores[state];
            if (lookaheadFiltersCache[state] is { } filters)
            {
                return filters;
            }
            var annotations = annotationList.GetAnnotations(state);
            if (annotations.IsEmpty)
            {
                return null;
            }
            filters = NewLookaheadSet(newStates[state].KernelItems.Count);
            foreach (var annotation in annotations)
            {
                Debug.Assert(annotation.StateIndex == state);
                var conflictSymbol = conflicts[annotation.ConflictIndex].Symbol;
                foreach (var row in annotation.ContributionMatrix)
                {
                    if (row is null)
                    {
                        continue;
                    }
                    foreach (var column in row.Value)
                    {
                        filters[column][conflictSymbol] = true;
                    }
                }
            }
            lookaheadFiltersCache[state] = filters;
            return filters;
        }

        // TODO: Consider reusing bit arrays of merged candidate lookahead sets.
        TerminalSet[] NewLookaheadSet(int kernelItemCount)
        {
            var lookaheadSet = new TerminalSet[kernelItemCount];
            for (int i = 0; i < kernelItemCount; i++)
            {
                lookaheadSet[i] = new TerminalSet(syntax);
            }
            return lookaheadSet;
        }

        int AddState(int originalState, TerminalSet[]? lookaheads)
        {
            Debug.Assert(lookaheads is null || newStates[originalState].KernelItems.Count == lookaheads.Length);
            int newStateIndex = newStates.Count;
            var newState = newStates[originalState].Clone();
            foreach (var transition in newState.Transitions)
            {
                if (transition.Key.IsTerminal)
                {
                    continue;
                }
                newState.Transitions[transition.Key] = newGotos.Count;
                newGotos.Add(newGotos[transition.Value].WithFromState(newStateIndex));
            }

            newStates.Add(newState);
            lalrIsocores.Add(lalrIsocores[originalState]);
            isocoreNexts.Add(isocoreNexts[originalState]);
            isocoreNexts[originalState] = newStateIndex;
            itemLookaheadSets.Add(lookaheads);
            lookaheadsRecomputed.Add(true);
            return newStateIndex;
        }

        void UpdateTransition(int fromState, Symbol symbol, int toState)
        {
            var transitions = newStates[fromState].Transitions;
            if (symbol.IsTerminal)
            {
                transitions[symbol] = toState;
            }
            else
            {
                int gotoIndex = transitions[symbol];
                newGotos[gotoIndex] = newGotos[gotoIndex].WithToState(toState);
            }
        }

        int FollowTransition(KeyValuePair<Symbol, int> transition) =>
            transition.Key.IsTerminal ? transition.Value : newGotos[transition.Value].ToState;
    }
}
