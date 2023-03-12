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
