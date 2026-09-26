// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;

namespace Farkle.Builder.OperatorPrecedence;

/// <summary>
/// Provides precedence and associativity (P&amp;A) information for terminals and productions in a grammar.
/// </summary>
internal sealed class OperatorInfoProvider
{
    public OperatorScope OperatorScope { get; }

    // Maps symbols (terminals or nonterminals) to their builder object representation.
    private readonly IReadOnlyDictionary<EntityHandle, object> _objectMap;

    // Maps builder objects to their precedence level.
    private readonly Dictionary<object, int> _precedenceMap;

    // Caches the computed precedence level of symbol handles.
    // The precedence level is -1 if the symbol has no precedence information.
    private readonly Dictionary<EntityHandle, int> _symbolPrecedenceCache = [];

    private int TryGetPrecedenceSlow(EntityHandle handle)
    {
        int precedence;
        if (!handle.IsProduction)
        {
            return _objectMap.TryGetValue(handle, out var obj) && _precedenceMap.TryGetValue(obj, out precedence) ? precedence : -1;
        }
        // Handle productions. Unless the production has a contextual precedence token,
        // it assumes the P&A of the last terminal it has. This is what (Fs)Yacc does.
        var production = (IProduction)_objectMap[handle];
        if (production.PrecedenceToken is { } precedenceToken)
        {
            return _precedenceMap.TryGetValue(precedenceToken, out precedence) ? precedence : -1;
        }
        var members = production.Members;
        for (int i = members.Length - 1; i >= 0; i--)
        {
            ISymbolBase symbol = members[i].Symbol;
            if (symbol is INonterminal)
            {
                continue;
            }
            if (_precedenceMap.TryGetValue(symbol, out precedence))
            {
                return precedence;
            }
        }
        return -1;
    }

    public OperatorInfoProvider(OperatorScope operatorScope, IReadOnlyDictionary<EntityHandle, object> objectMap,
        IEqualityComparer<object> symbolIdentityObjectComparer, BuilderLogger log = default)
    {
        OperatorScope = operatorScope;
        _objectMap = objectMap;
        _precedenceMap = new Dictionary<object, int>(objectMap.Count, new OperatorSymbolEqualityComparer(symbolIdentityObjectComparer));
        for (int i = 0; i < operatorScope.AssociativityGroups.Length; i++)
        {
            foreach (var x in operatorScope.AssociativityGroups[i].Symbols)
            {
                ref int existingPrecedence = ref CollectionsMarshal.GetValueRefOrAddDefault(_precedenceMap, x, out bool exists);
                if (exists)
                {
                    if (existingPrecedence != i)
                    {
                        log.DuplicateOperatorSymbol(x, existingPrecedence, i);
                    }
                }
                else
                {
                    existingPrecedence = i;
                }
            }
        }
    }

    public int GetPrecedence(EntityHandle symbol)
    {
        Debug.Assert(symbol.IsTokenSymbol || symbol.IsProduction);
        if (!_symbolPrecedenceCache.TryGetValue(symbol, out int precedence))
        {
            precedence = TryGetPrecedenceSlow(symbol);
            _symbolPrecedenceCache.Add(symbol, precedence);
        }
        return precedence;
    }

    /// <summary>
    /// Compares two operator symbols for equality.
    /// </summary>
    /// <param name="symbolIdentityObjectComparer">The equality comparer to use for comparing the identity objects of symbols.</param>
    /// <remarks>
    /// If the symbols are of type <see cref="IGrammarSymbol"/>, their identity
    /// objects are compared. Otherwise, the objects themselves are compared
    /// according to the rules of <see cref="Utilities.GetFallbackStringComparer"/>.
    /// </remarks>
    private sealed class OperatorSymbolEqualityComparer(IEqualityComparer<object> symbolIdentityObjectComparer) : IEqualityComparer<object>
    {
        [return: NotNullIfNotNull(nameof(x))]
        private static object? GetBuilderIdentityObject(object? x) => x switch
        {
            IGrammarSymbol symbol => GrammarDefinition.GetSymbolIdentityObject(symbol.Symbol),
            _ => x
        };

        public new bool Equals(object? x, object? y) =>
            symbolIdentityObjectComparer.Equals(GetBuilderIdentityObject(x), GetBuilderIdentityObject(y));

        public int GetHashCode(object obj) =>
            symbolIdentityObjectComparer.GetHashCode(GetBuilderIdentityObject(obj));
    }
}
