// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Farkle.Collections;

/// <summary>
/// Represents a collection of items that are sorted by an integer key.
/// </summary>
/// <remarks>
/// Keys are expected to be sequential integers starting from zero.
/// The collection has a linear memory overhead in terms of the number of possible key values.
/// Multiple items can have the same key.
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
internal readonly struct GroupedIndexedList<T> : IReadOnlyCollection<T>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly int[] _firstItem;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly ImmutableArray<T> _items;

    public int Count => _items.Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates a <see cref="GroupedIndexedList{T}"/>.
    /// </summary>
    /// <param name="keyCount">The number of possible key values. Keys are expected to be sequential integers starting from zero.</param>
    /// <param name="items">An immutable array containing the items. Items must be sorted by the result of <paramref name="getKey"/>.</param>
    /// <param name="getKey">A function that returns the key for a given item.</param>
    public GroupedIndexedList(int keyCount, ImmutableArray<T> items, Func<T, int> getKey)
    {
        _items = items;

        _firstItem = new int[keyCount];
        int previousItemKey = 0;
        int i;
        for (i = 0; i < _items.Length; i++)
        {
            int key = getKey(_items[i]);
            Debug.Assert(previousItemKey <= key + 1 && key < keyCount);
            while (previousItemKey < key + 1)
            {
                _firstItem[previousItemKey++] = i;
            }
        }
        while (previousItemKey < _firstItem.Length)
        {
            _firstItem[previousItemKey++] = i;
        }
    }

    /// <summary>
    /// Gets the items with the specified key.
    /// </summary>
    public ReadOnlySpan<T> GetItemsWithKey(int key)
    {
        int firstItem = _firstItem[key];
        int firstItemOfNext = key + 1 < _firstItem.Length ? _firstItem[key + 1] : _items.Length;
        int itemCount = firstItemOfNext - firstItem;
        return _items.AsSpan().Slice(firstItem, itemCount);
    }

    /// <summary>
    /// Returns an enumerator over the items with the specified key.
    /// </summary>
    /// <remarks>
    /// Using <see cref="GetItemsWithKey"/> should be preferred over this method, except in cases
    /// where ref structs cannot be used, such as when enumerating items in an iterator or an async method.
    /// </remarks>
    public ItemEnumerator EnumerateItemsWithKey(int key)
    {
        int firstItem = _firstItem[key];
        int firstItemOfNext = key + 1 < _firstItem.Length ? _firstItem[key + 1] : _items.Length;
        int itemCount = firstItemOfNext - firstItem;
        // ImmutableCollectionsMarshal.AsArray is safe here because we do not expose a
        // way to mutate the array. We use ArraySegment<T>.Enumerator to avoid duplicating
        // its logic.
        var segment = new ArraySegment<T>(ImmutableCollectionsMarshal.AsArray(_items)!, firstItem, itemCount);
        return new(segment.GetEnumerator());
    }

    public struct ItemEnumerator(ArraySegment<T>.Enumerator enumerator) : IEnumerable<T>, IEnumerator<T>
    {
        public readonly IEnumerator<T> GetEnumerator() => this;

        readonly IEnumerator IEnumerable.GetEnumerator() => this;

        public T Current => enumerator.Current;

        object? IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext() => enumerator.MoveNext();

        public void Reset()
        {
            ResetHelper(ref enumerator);

            static void ResetHelper<TEnumerator>(ref TEnumerator enumerator) where TEnumerator : struct, IEnumerator<T> => enumerator.Reset();
        }
    }
}
