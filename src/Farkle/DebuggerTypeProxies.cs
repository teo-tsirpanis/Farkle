// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle;

[ExcludeFromCodeCoverage]
internal class FlatCollectionProxy<T, TCollection>(TCollection list) where TCollection : IEnumerable<T>
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public readonly T[] _items = list.ToArray();
}

[DebuggerDisplay("{Value,nq}", Name = "{Name,nq}")]
[ExcludeFromCodeCoverage]
internal readonly struct NameValuePair(string name, string value)
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public readonly string Name = name, Value = value;
}
