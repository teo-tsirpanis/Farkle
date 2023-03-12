// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

global using Farkle.DebuggerTypeProxies;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using System.Diagnostics;

namespace Farkle.DebuggerTypeProxies;

internal class FlatCollectionProxy<T, TCollection> where TCollection : IEnumerable<T>
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public readonly T[] _items;

    public FlatCollectionProxy(TCollection list) => _items = list.ToArray();
}

internal class DfaProxy<TChar> : FlatCollectionProxy<DfaState<TChar>, Dfa<TChar>>
{
    public DfaProxy(Dfa<TChar> dfa) : base(dfa) { }
}

internal class DfaAcceptSymbolsProxy<TChar> : FlatCollectionProxy<TokenSymbolHandle, DfaState<TChar>.AcceptSymbolCollection>
{
    public DfaAcceptSymbolsProxy(DfaState<TChar>.AcceptSymbolCollection collection) : base(collection) { }
}

internal class DfaEdgesProxy<TChar> : FlatCollectionProxy<DfaEdge<TChar>, DfaState<TChar>.EdgeCollection>
{
    public DfaEdgesProxy(DfaState<TChar>.EdgeCollection collection) : base(collection) { }
}

[DebuggerDisplay("{Value,nq}", Name = "{Name,nq}")]
internal readonly struct NameValuePair
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public readonly string Name, Value;

    public NameValuePair(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

internal sealed class LrStateProxy
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly NameValuePair[] _actions;

    public LrStateProxy(LrState state)
    {
        _actions = new NameValuePair[state.Actions.Count + state.EndOfFileActions.Count + state.Gotos.Count];

        Grammar grammar = state.Grammar;
        int i = 0;
        foreach (var action in state.Actions)
        {
            TokenSymbol terminal = grammar.GetTokenSymbol(action.Key);
            _actions[i++] = new NameValuePair(terminal.ToString(), action.Value.ToString(grammar));
        }
        foreach (var action in state.EndOfFileActions)
        {
            _actions[i++] = new NameValuePair("(EOF)", action.ToString(grammar));
        }
        foreach (var @goto in state.Gotos)
        {
            Nonterminal nonterminal = grammar.GetNonterminal(@goto.Key);
            _actions[i++] = new NameValuePair(nonterminal.ToString(), $"Goto state {@goto.Value}");
        }
        Debug.Assert(i == _actions.Length);
    }
}

internal sealed class DfaStateProxy<TChar>
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly NameValuePair[] _actions;

    public DfaStateProxy(DfaState<TChar> state)
    {
        int defaultTransition = state.DefaultTransition;

        _actions = new NameValuePair[state.Edges.Count + (defaultTransition != -1 ? 1 : 0) + state.AcceptSymbols.Count];

        Grammar grammar = state.Grammar;
        int i = 0;
        foreach (var edge in state.Edges)
        {
            string key = EqualityComparer<TChar>.Default.Equals(edge.KeyFrom, edge.KeyTo)
                ? $"{DfaEdge<TChar>.Format(edge.KeyFrom)}"
                : $"[{DfaEdge<TChar>.Format(edge.KeyFrom)},{DfaEdge<TChar>.Format(edge.KeyTo)}]";
            string value = edge.Target < 0 ? "Fail" : $"Goto state {edge.Target}";
            _actions[i++] = new NameValuePair(key, value);
        }
        if (defaultTransition >= 0)
        {
            _actions[i++] = new NameValuePair(i > 0 ? "In all other cases" : "Always", $"Goto state {defaultTransition}");
        }
        foreach (var accept in state.AcceptSymbols)
        {
            _actions[i++] = new NameValuePair("Accept", grammar.GetTokenSymbol(accept).ToString());
        }
    }
}
