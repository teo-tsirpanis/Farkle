// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
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

    #region Legacy conflict resolution APIs; to be replaced with ConflictResolverNeo
    internal static LrConflictResolverDecision ResolveConflict(OperatorInfoProvider infoProvider, TokenSymbolHandle symbol, LrAction action1, LrAction action2)
    {
        Debug.Assert(!action1.IsError);
        Debug.Assert(!action2.IsError);
        Debug.Assert(!(action1.IsShift && action2.IsShift), "Shift/Shift conflict is not possible");

        var isShiftReduce = action1.IsShift || action2.IsShift;

        if (!isShiftReduce && !infoProvider.OperatorScope.CanResolveReduceReduceConflicts)
        {
            return LrConflictResolverDecision.CannotChoose;
        }

        var precedence1 = infoProvider.GetPrecedence(action1.IsShift ? symbol : action1.ReduceProduction);
        var precedence2 = infoProvider.GetPrecedence(action2.IsShift ? symbol : action2.ReduceProduction);

        if (precedence1 < 0 || precedence2 < 0)
        {
            return LrConflictResolverDecision.CannotChoose;
        }
        if (precedence1 > precedence2)
        {
            return LrConflictResolverDecision.ChooseOption1;
        }
        if (precedence1 < precedence2)
        {
            return LrConflictResolverDecision.ChooseOption2;
        }
        if (isShiftReduce)
        {
            switch (infoProvider.OperatorScope.AssociativityGroups[precedence1].AssociativityType)
            {
                case AssociativityType.LeftAssociative:
                    return action1.IsReduce ? LrConflictResolverDecision.ChooseOption1 : LrConflictResolverDecision.ChooseOption2;
                case AssociativityType.RightAssociative:
                    return action1.IsShift ? LrConflictResolverDecision.ChooseOption1 : LrConflictResolverDecision.ChooseOption2;
                case AssociativityType.NonAssociative:
                    return LrConflictResolverDecision.ChooseNeither;
            }
        }
        return LrConflictResolverDecision.CannotChoose;
    }

    internal static LrConflictResolverDecision ResolveEndOfFileConflict(OperatorInfoProvider infoProvider, LrEndOfFileAction action1, LrEndOfFileAction action2)
    {
        Debug.Assert(!action1.IsError);
        Debug.Assert(!action2.IsError);

        if (action1.IsAccept || action2.IsAccept)
        {
            Debug.Assert(!(action1.IsAccept && action2.IsAccept), "Accept/Accept conflict is not possible");
            // Accept/Reduce conflicts cannot be resolved.
            return LrConflictResolverDecision.CannotChoose;
        }

        if (!infoProvider.OperatorScope.CanResolveReduceReduceConflicts)
        {
            return LrConflictResolverDecision.CannotChoose;
        }

        var precedence1 = infoProvider.GetPrecedence(action1.ReduceProduction);
        var precedence2 = infoProvider.GetPrecedence(action2.ReduceProduction);

        if (precedence1 < 0 || precedence2 < 0)
        {
            return LrConflictResolverDecision.CannotChoose;
        }
        if (precedence1 > precedence2)
        {
            return LrConflictResolverDecision.ChooseOption1;
        }
        if (precedence1 < precedence2)
        {
            return LrConflictResolverDecision.ChooseOption2;
        }
        return LrConflictResolverDecision.CannotChoose;
    }
    #endregion

    /// <summary>
    /// Contains the logic to resolve an LR conflict.
    /// </summary>
    /// <remarks>
    /// This is a stateful value type that keeps track of a figurative set of dominant contributions to the conflict
    /// (known as the <em>dominant set</em>). The complete dominant set is not held by this type; for each contribution
    /// that gets considered, the resolver indicates to the caller what operation to perform to the dominant set.
    /// </remarks>
    private struct ConflictResolverNeo(OperatorInfoProvider? infoProvider)
    {
        private readonly OperatorInfoProvider? _infoProvider = infoProvider;

        private int _dominantPrecedence = -1;

        private AssociativityType _dominantAssociativity;

        private bool _hasShift, _hasShiftInDominantSet, _hasRemovedShiftFromDominantSet;

#if DEBUG
        private bool _seenContribution, _seenAccept;
#endif

        /// <summary>
        /// Records a <see cref="LrConflictContribution"/> to the conflict and updates the dominant set accordingly.
        /// </summary>
        /// <param name="conflictSymbol">The symbol at which the conflict occurs.</param>
        /// <param name="contribution">The contribution to the conflict being considered.</param>
        public ConflictResolutionResult Add(Symbol conflictSymbol, LrConflictContribution contribution)
        {
#if DEBUG
            // This also detects Shift/Shift conflicts, which are not possible.
            Debug.Assert(!contribution.IsShift(out _) || !_seenContribution, "The Shift contribution must be added before any other contributions");
            _seenContribution = true;
#endif
            if (contribution.IsShift(out _))
            {
                Debug.Assert(conflictSymbol.Index != EndSymbolIndex);
                _hasShift = true;
            }

            if (contribution.IsAccept)
            {
#if DEBUG
                Debug.Assert(!_seenAccept, "Accept/Accept conflict is not possible");
                _seenAccept = true;
#endif
                return ConflictResolutionResult.NoPrecedence;
            }
            if (_infoProvider is null)
            {
                return ConflictResolutionResult.NoPrecedence;
            }
            int precedence = _infoProvider.GetPrecedence(contribution.IsReduce(out Production production) ? TranslateProduction(production) : TranslateTerminal(conflictSymbol));
            if (precedence < 0)
            {
                // The contribution has no precedence information.
                return ConflictResolutionResult.NoPrecedence;
            }
            if (_dominantPrecedence == -1)
            {
                // This is the first contribution that we see.
                _dominantPrecedence = precedence;
                if (contribution.IsShift(out _))
                {
                    _dominantAssociativity = _infoProvider.OperatorScope.AssociativityGroups[precedence].AssociativityType;
                    _hasShiftInDominantSet = true;
                }
                return ConflictResolutionResult.CreateNewDominantSet;
            }
            if (!_hasShift && !_infoProvider.OperatorScope.CanResolveReduceReduceConflicts)
            {
                // The operator scope cannot resolve Reduce/Reduce conflicts.
                return ConflictResolutionResult.NoPrecedence;
            }
            if (precedence < _dominantPrecedence)
            {
                return ConflictResolutionResult.Ignore;
            }
            if (precedence == _dominantPrecedence)
            {
                if (_hasShiftInDominantSet)
                {
                    switch (_dominantAssociativity)
                    {
                        case AssociativityType.RightAssociative:
                            return ConflictResolutionResult.Ignore;
                        case AssociativityType.LeftAssociative when !_hasRemovedShiftFromDominantSet:
                            // If we have a shift-reduce conflict with same precedence and left associativity, we
                            _hasRemovedShiftFromDominantSet = true;
                            return ConflictResolutionResult.CreateNewDominantSet;
                        case AssociativityType.NonAssociative:
                            return ConflictResolutionResult.ClearDominantSet;
                    }
                }
                return ConflictResolutionResult.AddToDominantSet;
            }
            _dominantPrecedence = precedence;
            if (!_hasShift)
            {
                // We don't care about associativity in Reduce/Reduce conflicts.
                _dominantAssociativity = _infoProvider.OperatorScope.AssociativityGroups[precedence].AssociativityType;
            }
            _hasShiftInDominantSet = false;
            _hasRemovedShiftFromDominantSet = false;
            return ConflictResolutionResult.CreateNewDominantSet;
        }
    }

    /// <summary>
    /// Represents how a conflict contribution affects the formation of the dominant set.
    /// </summary>
    private enum ConflictResolutionResult
    {
        /// <summary>
        /// The contribution should be ignored.
        /// </summary>
        Ignore,
        /// <summary>
        /// The contribution should be added to the dominant set.
        /// </summary>
        AddToDominantSet,
        /// <summary>
        /// The contribution should create a new dominant set.
        /// </summary>
        CreateNewDominantSet,
        /// <summary>
        /// The contribution should clear the dominant set.
        /// </summary>
        ClearDominantSet,
        /// <summary>
        /// The contribution has no precedence. All contributions, including those previously
        /// discarded, are included in the dominant set.
        /// </summary>
        NoPrecedence,
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
