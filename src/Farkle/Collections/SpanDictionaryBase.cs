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
    /// <typeparam name="TKey">The type of the key's items.</typeparam>
    /// <typeparam name="TContainer">The type used to store the key in the heap. Must be immutable.
    /// Typically it is <see cref="string"/> or <see cref="ImmutableArray{TKey}"/></typeparam>
    /// <typeparam name="TValue">The type of values this dictionary holds.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    internal abstract class SpanDictionaryBase<TKey, TContainer, TValue> where TKey : struct, IEquatable<TKey>
    {
#if NET6_0_OR_GREATER
        private readonly Dictionary<int, (TContainer Key, TValue Value)> _dictionary = new();
#else
        private readonly Dictionary<int, StrongBox<(TContainer Key, TValue Value)>> _dictionary = new();
#endif

        /// <summary>
        /// Computes the hash code of the key.
        /// </summary>
        protected abstract int GetHashCode(ReadOnlySpan<TKey> key);

        /// <summary>
        /// Returns the content of a <typeparamref name="TContainer"/>.
        /// </summary>
        protected abstract ReadOnlySpan<TKey> AsSpan(TContainer container);

        /// <summary>
        /// Creates a <typeparamref name="TContainer"/> from a key.
        /// </summary>
        protected abstract TContainer ToContainer(ReadOnlySpan<TKey> key);

        // A simple LCG. Constants taken from
        // https://github.com/imneme/pcg-c/blob/83252d9c23df9c82ecb42210afed61a7b42402d7/include/pcg_variants.h#L276-L284
        private static int GetNextDictionaryKey(int dictionaryKey) =>
            (int)((uint)dictionaryKey * 747796405 + 2891336453);

        private unsafe ref (TContainer Key, TValue Value) GetValueRefOrAddDefault(int dictionaryKey, out bool exists)
        {
#if NET6_0_OR_GREATER
#pragma warning disable CS9088 // This returns a parameter by reference but it is scoped to the current method
            // In .NET 6 the assembly of GetValueRefOrAddDefault was compiled with earlier ref safety rules
            // and caused an error, which was turned into a warning because of unsafe and was suppressed.
            return ref CollectionsMarshal.GetValueRefOrAddDefault(_dictionary, dictionaryKey, out exists);
#pragma warning restore CS9088 // This returns a parameter by reference but it is scoped to the current method
#else
            if (!(exists = _dictionary.TryGetValue(dictionaryKey, out var entry)))
            {
                entry = _dictionary[dictionaryKey] = new();
            }
            return ref entry.Value;
#endif
        }

        private ref (TContainer Key, TValue Value) GetValueRefOrAddDefault(ReadOnlySpan<TKey> key, out bool exists)
        {
            int dictionaryKey = GetHashCode(key);
            while (true)
            {
                ref var entry = ref GetValueRefOrAddDefault(dictionaryKey, out exists);
                if (!exists || AsSpan(entry.Key).SequenceEqual(key))
                {
                    return ref entry;
                }
                dictionaryKey = GetNextDictionaryKey(dictionaryKey);
            }
        }

        private ref (TContainer Key, TValue Value) GetValueRefOrNullRef(ReadOnlySpan<TKey> key)
        {
            int dictionaryKey = GetHashCode(key);
            while (true)
            {
                ref (TContainer Key, TValue Value) entry =
#if NET6_0_OR_GREATER
                    ref CollectionsMarshal.GetValueRefOrNullRef(_dictionary, dictionaryKey);
#else
                    ref _dictionary.TryGetValue(dictionaryKey, out var strongBox) ? ref strongBox.Value : ref Unsafe.NullRef<(TContainer, TValue)>();
#endif
                if (Unsafe.IsNullRef(ref entry) || AsSpan(entry.Key).SequenceEqual(key))
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
                    entry.Key = ToContainer(key);
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
            entry = (ToContainer(key), value);
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

        public TValue GetOrAdd(ReadOnlySpan<TKey> key, TValue value, out bool exists, out TContainer immutableKey)
        {
            ref var entry = ref GetValueRefOrAddDefault(key, out exists);
            if (!exists)
            {
                entry.Key = immutableKey = ToContainer(key);
                entry.Value = value;
            }
            else
            {
                immutableKey = entry.Key;
            }
            return entry.Value;
        }

        public TValue GetOrAdd(TContainer key, TValue value, out bool exists)
        {
            ref var entry = ref GetValueRefOrAddDefault(AsSpan(key), out exists);
            if (!exists)
            {
                entry.Key = key;
                entry.Value = value;
            }
            return entry.Value;
        }
    }
}
