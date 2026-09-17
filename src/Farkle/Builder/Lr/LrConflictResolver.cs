// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic to resolve an LR conflict.
/// </summary>
/// <remarks>
/// This is a stateful value type that keeps track of a figurative set of dominant contributions to the conflict
/// (known as the <em>dominant set</em>). The complete dominant set is not held by this type; for each contribution
/// that gets considered, the resolver indicates to the caller what operation to perform to the dominant set.
/// </remarks>
internal struct LrConflictResolver(OperatorInfoProvider? infoProvider)
{
    private readonly OperatorInfoProvider? _infoProvider = infoProvider;

    private int _dominantPrecedence = -1;

    private AssociativityType _dominantAssociativity;

    private bool _hasShift, _hasConflictInDominantSet, _hasShiftInDominantSet, _hasRemovedShiftFromDominantSet;

#if DEBUG
    private bool _seenContribution, _seenAccept;
#endif

    /// <summary>
    /// Records a <see cref="LrConflictContribution"/> to the conflict and updates the dominant set accordingly.
    /// </summary>
    /// <param name="conflictSymbol">The symbol at which the conflict occurs.</param>
    /// <param name="contribution">The contribution to the conflict being considered.</param>
    public LrConflictResolverDecision Add(Symbol conflictSymbol, LrConflictContribution contribution) =>
        Add(conflictSymbol, contribution, out _);

    /// <summary>
    /// Records a <see cref="LrConflictContribution"/> to the conflict and updates the dominant set accordingly.
    /// </summary>
    /// <param name="conflictSymbol">The symbol at which the conflict occurs.</param>
    /// <param name="contribution">The contribution to the conflict being considered.</param>
    /// <param name="precedence">The precedence of <paramref name="contribution"/>, or -1 if it has no precedence information.</param>
    /// <remarks></remarks>
    public LrConflictResolverDecision Add(Symbol conflictSymbol, LrConflictContribution contribution, out int precedence)
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
            precedence = -1;
            return LrConflictResolverDecision.NoPrecedence;
        }
        if (_infoProvider is null)
        {
            precedence = -1;
            return LrConflictResolverDecision.NoPrecedence;
        }
        precedence = _infoProvider.GetPrecedence(contribution.IsReduce(out Production production) ? TranslateProduction(production) : TranslateTerminal(conflictSymbol));
        if (precedence < 0)
        {
            // The contribution has no precedence information.
            return LrConflictResolverDecision.NoPrecedence;
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
            else if (!_infoProvider.OperatorScope.CanResolveReduceReduceConflicts)
            {
                precedence = -1;
                return LrConflictResolverDecision.NoPrecedence;
            }
            return LrConflictResolverDecision.CreateNewDominantSet;
        }
        if (!_hasShift && !_infoProvider.OperatorScope.CanResolveReduceReduceConflicts)
        {
            // The operator scope cannot resolve Reduce/Reduce conflicts.
            precedence = -1;
            return LrConflictResolverDecision.NoPrecedence;
        }
        if (precedence < _dominantPrecedence)
        {
            return LrConflictResolverDecision.Ignore;
        }
        if (precedence == _dominantPrecedence)
        {
            _hasConflictInDominantSet = true;
            if (_hasShiftInDominantSet)
            {
                switch (_dominantAssociativity)
                {
                    case AssociativityType.RightAssociative:
                        return LrConflictResolverDecision.Ignore;
                    case AssociativityType.LeftAssociative when !_hasRemovedShiftFromDominantSet:
                        // If we have a shift-reduce conflict with same precedence and left associativity, we
                        _hasRemovedShiftFromDominantSet = true;
                        return LrConflictResolverDecision.CreateNewDominantSet;
                    case AssociativityType.NonAssociative:
                        return LrConflictResolverDecision.ClearDominantSet;
                }
            }
            return LrConflictResolverDecision.AddToDominantSet;
        }
        _dominantPrecedence = precedence;
        if (!_hasShift)
        {
            // We don't care about associativity in Reduce/Reduce conflicts.
            _dominantAssociativity = _infoProvider.OperatorScope.AssociativityGroups[precedence].AssociativityType;
        }
        _hasConflictInDominantSet = false;
        _hasShiftInDominantSet = false;
        _hasRemovedShiftFromDominantSet = false;
        return LrConflictResolverDecision.CreateNewDominantSet;
    }

    /// <summary>
    /// Returns whether an <see cref="LrConflictContribution"/> is contained in the dominant set.
    /// The contribution must have been previously passed to <see cref="Add(Symbol, LrConflictContribution, out int)"/>.
    /// </summary>
    public readonly bool ContainsInDominantSet(LrConflictContribution contribution, int precedence)
    {
        if (precedence < 0)
        {
            return true;
        }
        if (precedence < _dominantPrecedence)
        {
            return false;
        }
        Debug.Assert(precedence == _dominantPrecedence);
        if (_hasConflictInDominantSet && _hasShiftInDominantSet)
        {
            switch (_dominantAssociativity)
            {
                case AssociativityType.LeftAssociative:
                    return contribution.IsReduce(out _);
                case AssociativityType.RightAssociative:
                    return !contribution.IsReduce(out _);
                case AssociativityType.NonAssociative:
                    return false;
            }
        }
        return true;
    }
}
