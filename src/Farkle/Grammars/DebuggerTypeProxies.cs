// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars.StateMachines;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars;

[ExcludeFromCodeCoverage]
internal class DfaProxy<TChar>(Dfa<TChar> dfa) : FlatCollectionProxy<DfaState<TChar>, Dfa<TChar>>(dfa);

[ExcludeFromCodeCoverage]
internal class DfaAcceptSymbolsProxy<TChar>(DfaState<TChar>.AcceptSymbolCollection collection) : FlatCollectionProxy<TokenSymbolDefinition, DfaState<TChar>.AcceptSymbolCollection>(collection);

[ExcludeFromCodeCoverage]
internal class DfaEdgesProxy<TChar>(DfaState<TChar>.EdgeCollection collection) : FlatCollectionProxy<DfaEdge<TChar>, DfaState<TChar>.EdgeCollection>(collection);

[ExcludeFromCodeCoverage]
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
            _actions[i++] = new NameValuePair(action.Key.ToString(), action.Value.ToString(grammar));
        }
        foreach (var action in state.EndOfFileActions)
        {
            _actions[i++] = new NameValuePair("(EOF)", action.ToString(grammar));
        }
        foreach (var @goto in state.Gotos)
        {
            _actions[i++] = new NameValuePair(@goto.Key.ToString(), $"Goto state {@goto.Value}");
        }
        Debug.Assert(i == _actions.Length);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class DfaStateProxy<TChar>
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly NameValuePair[] _actions;

    public DfaStateProxy(DfaState<TChar> state)
    {
        int defaultTransition = state.DefaultTransition;

        _actions = new NameValuePair[state.Edges.Count + (defaultTransition != -1 ? 1 : 0) + state.AcceptSymbols.Count];

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
            string name = i > 0 ? "In all other cases" : "Always";
            _actions[i++] = new NameValuePair(name, $"Goto state {defaultTransition}");
        }
        foreach (var accept in state.AcceptSymbols)
        {
            _actions[i++] = new NameValuePair("Accept", accept.ToString());
        }
    }
}
