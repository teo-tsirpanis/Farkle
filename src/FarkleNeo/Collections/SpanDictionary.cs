// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Farkle.Collections
{
    /// <summary>
    /// Efficiently stores key-value pairs where the key is of variable size.
    /// </summary>
    internal sealed class SpanDictionary<TKey, TValue> where TKey : struct, IEquatable<TKey>
    {
#if NET6_0_OR_GREATER
        private readonly Dictionary<int, (ImmutableArray<TKey> Key, TValue Value)> _dictionary = new();
#else
        private readonly Dictionary<int, StrongBox<(ImmutableArray<TKey> Key, TValue Value)>> _dictionary = new();
#endif

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

        // A simple LCG. Constants taken from
        // https://github.com/imneme/pcg-c/blob/83252d9c23df9c82ecb42210afed61a7b42402d7/include/pcg_variants.h#L276-L284
        private static int GetNextDictionaryKey(int dictionaryKey) =>
            (int)((uint)dictionaryKey * 747796405 + 2891336453);

        private ref (ImmutableArray<TKey> Key, TValue Value) GetValueRefOrAddDefault(int dictionaryKey, [UnscopedRef] out bool exists)
        {
#if NET6_0_OR_GREATER
            return ref CollectionsMarshal.GetValueRefOrAddDefault(_dictionary, dictionaryKey, out exists);
#else
            if (!(exists = _dictionary.TryGetValue(dictionaryKey, out var entry)))
            {
                entry = _dictionary[dictionaryKey] = new();
            }
            return ref entry.Value;
#endif
        }

        private ref (ImmutableArray<TKey> Key, TValue Value) GetValueRefOrAddDefault(ReadOnlySpan<TKey> key, [UnscopedRef] out bool exists)
        {
            int dictionaryKey = GetHashCode(key);
            while (true)
            {
                ref var entry = ref GetValueRefOrAddDefault(dictionaryKey, out exists);
                if (!exists || entry.Key.AsSpan().SequenceEqual(key))
                {
                    return ref entry;
                }
                dictionaryKey = GetNextDictionaryKey(dictionaryKey);
            }
        }

        private ref (ImmutableArray<TKey> Key, TValue Value) GetValueRefOrNullRef(ReadOnlySpan<TKey> key)
        {
            int dictionaryKey = GetHashCode(key);
            while (true)
            {
                ref var entry =
#if NET6_0_OR_GREATER
                    ref CollectionsMarshal.GetValueRefOrNullRef(_dictionary, dictionaryKey);
#else
                    ref _dictionary.TryGetValue(dictionaryKey, out var strongBox) ? ref strongBox.Value : ref Unsafe.NullRef<(ImmutableArray<TKey>, TValue)>();
#endif
                if (Unsafe.IsNullRef(ref entry) || entry.Key.AsSpan().SequenceEqual(key))
                {
                    return ref entry;
                }
                dictionaryKey = GetNextDictionaryKey(dictionaryKey);
            }
        }

        public int Count => _dictionary.Count;

        public TValue this[ReadOnlySpan<TKey> key]
        {
            get
            {
                ref var entry = ref GetValueRefOrNullRef(key);
                if (Unsafe.IsNullRef(ref entry))
                {
                    ThrowHelpers.ThrowKeyNotFoundException();
                }
                return entry.Value;
            }
            set
            {
                ref var entry = ref GetValueRefOrAddDefault(key, out bool exists);
                if (!exists)
                {
                    Debug.Assert(entry.Key.IsDefault);
                    entry.Key = key.ToImmutableArray();
                }
                entry.Value = value;
            }
        }

        public void Add(ReadOnlySpan<TKey> key, TValue value)
        {
            ref var entry = ref GetValueRefOrAddDefault(key, out bool exists);
            if (exists)
            {
                ThrowHelpers.ThrowArgumentException(nameof(key), "An element with the same key already exists in the dictionary.");
            }
            entry = (key.ToImmutableArray(), value);
        }

        public bool TryGetValue(ReadOnlySpan<TKey> key, [MaybeNullWhen(false)] out TValue value)
        {
            ref var entry = ref GetValueRefOrNullRef(key);
            if (!Unsafe.IsNullRef(ref entry))
            {
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }
    }
}
