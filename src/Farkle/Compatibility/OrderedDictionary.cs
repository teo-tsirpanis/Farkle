// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET9_0_OR_GREATER
namespace System.Collections.Generic;

internal sealed class OrderedDictionary<TKey, TValue>(IEqualityComparer<TKey>? comparer = null) : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private readonly Dictionary<TKey, int> _items = new(comparer);

    private readonly List<KeyValuePair<TKey, TValue>> _orderedItems = [];

    public void Clear()
    {
        _items.Clear();
        _orderedItems.Clear();
    }

    public KeyValuePair<TKey, TValue> GetAt(int index) => _orderedItems[index];

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        _orderedItems.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryAdd(TKey key, TValue value, out int index)
    {
        if (_items.TryAdd(key, _orderedItems.Count))
        {
            _orderedItems.Add(new(key, value));
            index = _orderedItems.Count - 1;
            return true;
        }

        index = _items[key];
        return false;
    }
}
#elif NET9_0
namespace System.Collections.Generic;

internal static class OrderedDictionaryCompat
{
    public static bool TryAdd<TKey, TValue>(this OrderedDictionary<TKey, TValue> dictionary, TKey key, TValue value, out int index) where TKey : notnull
    {
        bool result = dictionary.TryAdd(key, value);
        index = dictionary.IndexOf(key);
        return result;
    }
}
#endif
