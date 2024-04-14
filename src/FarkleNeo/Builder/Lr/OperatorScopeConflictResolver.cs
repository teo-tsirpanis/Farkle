// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;

namespace Farkle.Builder.Lr;

/// <summary>
/// Provides an implementation of <see cref="LrConflictResolver"/> that resolves
/// conflicts based on operator precedence and associativity (P&amp;A).
/// </summary>
internal sealed class OperatorScopeConflictResolver : LrConflictResolver
{
    private readonly OperatorScope _operatorScope;

    // Maps symbols (terminals or nonterminals) to their builder object representation.
    private readonly IReadOnlyDictionary<EntityHandle, object> _objectMap;

    // Maps builder objects to their precedence level.
    private readonly Dictionary<object, int> _precedenceMap;

    private const LrConflictResolverDecision ChooseShift = LrConflictResolverDecision.ChooseOption1;

    private const LrConflictResolverDecision ChooseReduce = LrConflictResolverDecision.ChooseOption2;

    private bool TryGetSymbolObject(EntityHandle handle, [MaybeNullWhen(false)] out object productionObject)
    {
        if (!handle.IsProduction)
        {
            return _objectMap.TryGetValue(handle, out productionObject);
        }
        // Handle productions. Unless the production has a contextual precedence token,
        // it assumes the P&A of the last terminal it has. This is what (Fs)Yacc does.
        var production = (IProduction)_objectMap[handle];
        if (production.PrecedenceToken is { } precedenceToken)
        {
            productionObject = precedenceToken;
            return true;
        }
        var members = production.Members;
        for (int i = members.Length - 1; i >= 0; i--)
        {
            ISymbolBase symbol = members[i].Symbol;
            // TODO: Remove this if block (see #41).
            if (symbol is INonterminal)
            {
                continue;
            }
            if (_precedenceMap.ContainsKey(symbol))
            {
                productionObject = symbol;
                return true;
            }
        }
        productionObject = null;
        return false;
    }

    private bool TryGetPrecedenceInfo(EntityHandle symbol,
        out int precedence, out AssociativityType associativity)
    {
        if (!TryGetSymbolObject(symbol, out object? symbolObject) || !_precedenceMap.TryGetValue(symbolObject, out precedence))
        {
            precedence = 0;
            associativity = AssociativityType.NonAssociative;
            return false;
        }
        associativity = _operatorScope.AssociativityGroups[precedence].AssociativityType;
        return true;
    }

    public OperatorScopeConflictResolver(OperatorScope operatorScope, IReadOnlyDictionary<EntityHandle, object> objectMap,
        bool literalsCaseSensitive, BuilderLogger log = default)
    {
        _operatorScope = operatorScope;
        _objectMap = objectMap;
        _precedenceMap = new Dictionary<object, int>(objectMap.Count, new OperatorSymbolEqualityComparer(literalsCaseSensitive));
        for (int i = 0; i < operatorScope.AssociativityGroups.Length; i++)
        {
            foreach (var x in operatorScope.AssociativityGroups[i].Symbols)
            {
                if (!_precedenceMap.TryAdd(x, i) && _precedenceMap[x] != i)
                {
                    // TODO: Log a warning that the same operator is defined in multiple associativity groups.
                }
            }
        }
    }

    public override LrConflictResolverDecision ResolveShiftReduceConflict(TokenSymbolHandle shiftTerminal, ProductionHandle reduceProduction)
    {
        if (!TryGetPrecedenceInfo(shiftTerminal, out int shiftPrecedence, out AssociativityType shiftAssociativity)
            || !TryGetPrecedenceInfo(reduceProduction, out int reducePrecedence, out AssociativityType reduceAssociativity))
        {
            return LrConflictResolverDecision.CannotChoose;
        }
        if (shiftPrecedence > reducePrecedence)
        {
            return ChooseShift;
        }
        if (shiftPrecedence < reducePrecedence)
        {
            return ChooseReduce;
        }
        // If the symbols have the same precedence, we resolve the conflict based on associativity.
        // The symbols are on the same associativity group, so they have the same associativity.
        Debug.Assert(shiftAssociativity == reduceAssociativity);
        switch (shiftAssociativity)
        {
            case AssociativityType.LeftAssociative:
                return ChooseReduce;
            case AssociativityType.RightAssociative:
                return ChooseShift;
            case AssociativityType.NonAssociative:
                return LrConflictResolverDecision.ChooseNeither;
            default:
                Debug.Assert(shiftAssociativity == AssociativityType.PrecedenceOnly);
                return LrConflictResolverDecision.CannotChoose;
        }
    }

    public override LrConflictResolverDecision ResolveReduceReduceConflict(ProductionHandle production1, ProductionHandle production2)
    {
        if (!_operatorScope.CanResolveReduceReduceConflicts)
        {
            return LrConflictResolverDecision.CannotChoose;
        }
        if (!TryGetPrecedenceInfo(production1, out int precedence1, out _)
            || !TryGetPrecedenceInfo(production2, out int precedence2, out _))
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

    /// <summary>
    /// Compares two operator symbols for equality.
    /// </summary>
    /// <param name="literalsCaseSensitive">Whether literals in the grammar
    /// are case-sensitive.</param>
    /// <remarks>
    /// If the symbols are of type <see cref="IGrammarSymbol"/>, their identity
    /// objects are compared. Otherwise, the objects themselves are compared
    /// according to the rules of <see cref="Utilities.GetFallbackStringComparer"/>.
    /// </remarks>
    private sealed class OperatorSymbolEqualityComparer(bool literalsCaseSensitive) : IEqualityComparer<object>
    {
        private readonly IEqualityComparer<object> _comparer = Utilities.GetFallbackStringComparer(literalsCaseSensitive);

        [return: NotNullIfNotNull(nameof(x))]
        private static object? GetBuilderIdentityObject(object? x) => x switch
        {
            IGrammarSymbol symbol => GrammarDefinition.GetSymbolIdentityObject(symbol.Symbol),
            _ => x
        };

#pragma warning disable CS8604 // Possible null reference argument.
        // In .NET Standard 2.1 Equals' parameters are not marked as nullable.
        public new bool Equals(object? x, object? y) =>
            _comparer.Equals(GetBuilderIdentityObject(x), GetBuilderIdentityObject(y));
#pragma warning restore CS8604 // Possible null reference argument.

        public int GetHashCode(object obj) =>
            _comparer.GetHashCode(GetBuilderIdentityObject(obj));
    }
}
