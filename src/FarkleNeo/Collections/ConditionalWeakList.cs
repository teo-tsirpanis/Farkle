// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NET6_0_OR_GREATER
using System.Collections;
using System.Runtime.CompilerServices;

namespace Farkle.Collections;

/// <summary>
/// Stores a list of weak references to objects.
/// </summary>
internal sealed class ConditionalWeakList<T> : IEnumerable<T> where T : class
{
    // This could use a list or array of weak references, but it's not very important
    // to optimize, considering the development-only nature of Hot Reload.
    private readonly ConditionalWeakTable<T, object> _container = [];

    public void Add(T item)
    {
        _container.Add(item, item);
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var entry in _container)
        {
            yield return entry.Key;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
#endif
