// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Farkle.Collections
{
    /// <summary>
    /// Efficiently stores key-value pairs where the key is of variable size.
    /// </summary>
    internal sealed class SpanDictionary<TKey, TValue> where TKey : struct, IEquatable<TKey>
    {
        // I feared I'd have to create a full-featured reimplementation of Dictionary, but then thought
        // that adding just an extra level of indirection avoids most of the work, with better performance
        // than what System.Reflection.Metadata does for its blob heap.
        // PERF: We still go through quite some hoops to access our data. I don't think it will pose a
        // bottleneck, and I really hope so.
        private readonly Dictionary<int, List<KeyValuePair<ImmutableArray<TKey>, TValue>>> _dictionary = new();

        private static int GetHashCode(ReadOnlySpan<TKey> key)
        {
            HashCode hashCode = new();
            // Type.IsPrimitive is not a JIT intrinsic; explicitly check for common types before.
            if (typeof(TKey) == typeof(byte) || typeof(TKey) == typeof(char) || typeof(TKey).IsPrimitive)
            {
                hashCode.AddBytes(MemoryMarshal.AsBytes(key));
            }
            else
            {
                foreach (TKey k in key)
                {
                    hashCode.Add(k);
                }
            }
            return hashCode.ToHashCode();
        }

        private static int FindInList(List<KeyValuePair<ImmutableArray<TKey>, TValue>> list, ReadOnlySpan<TKey> key)
        {
            int i = 0;

#if NET6_0_OR_GREATER
            foreach (ref var kvp in CollectionsMarshal.AsSpan(list))
#else
            foreach (var kvp in list)
#endif
            {
                if (kvp.Key.AsSpan().SequenceEqual(key))
                {
                    return i;
                }
                i++;
            }

            return -1;
        }

        private List<KeyValuePair<ImmutableArray<TKey>, TValue>> GetOrCreateList(int hashCode)
        {
#if NET6_0_OR_GREATER
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_dictionary, hashCode, out _);
            // Collisions are unlikely across the full 32-bit range, so allocate a list with only one element,
            // instead of the default four.
            list ??= new(capacity: 1);
            return list;
#else
            if (_dictionary.TryGetValue(hashCode, out var list))
            {
                return list;
            }
            list = new(capacity: 1);
            _dictionary.Add(hashCode, list);
            return list;
#endif
        }

        public int Count { get; private set; }

        public TValue this[ReadOnlySpan<TKey> key]
        {
            get
            {
                if (!TryGetValue(key, out TValue? value))
                {
                    ThrowHelpers.ThrowKeyNotFoundException("Key not found.");
                }
                return value;
            }
            set
            {
                int hashCode = GetHashCode(key);
                var list = GetOrCreateList(hashCode);
                switch (FindInList(list, key))
                {
                    case < 0:
                        list.Add(new(key.ToImmutableArray(), value));
                        Count++;
                        break;
                    case int idx:
                        list[idx] = new(list[idx].Key, value);
                        break;
                }
            }
        }

        public void Add(ReadOnlySpan<TKey> key, TValue value)
        {
            int hashCode = GetHashCode(key);
            var list = GetOrCreateList(hashCode);
            if (FindInList(list, key) >= 0)
            {
                ThrowHelpers.ThrowArgumentException(nameof(key), "An element with the same key already exists in the dictionary.");
            }

            list.Add(new(key.ToImmutableArray(), value));
            Count++;
        }

        public bool TryGetValue(ReadOnlySpan<TKey> key, [MaybeNullWhen(false)] out TValue value)
        {
            int hashCode = GetHashCode(key);
            if (_dictionary.TryGetValue(hashCode, out var list))
            {
                int idx = FindInList(list, key);
                if (idx >= 0)
                {
                    value = list[idx].Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
