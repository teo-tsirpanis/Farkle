// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Farkle.Collections;

/// <summary>
/// A stack type that can store its items in stack-allocated memory.
/// </summary>
[DebuggerDisplay("Count = {Count}")]
internal ref struct ValueStack<T>
{
    private Span<T> _items;
    private T[]? _pooledArray;
    private int _count;

    private const int InitialCapacity = 4;

    private static bool ShouldResetItems =>
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
        // On .NET Standard 2.0 it might return false positives but that's fine.
        // We will use this value only for optimizations.
        !typeof(T).IsPrimitive;
#endif

    public ValueStack(int initialCapacity)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(initialCapacity);
        _items = _pooledArray = ArrayPool<T>.Shared.Rent(initialCapacity);
        _count = 0;
    }

    public ValueStack(Span<T> items)
    {
        _items = items;
        _pooledArray = null;
        _count = 0;
    }

    public ValueStack(State state)
    {
        _items = _pooledArray = state.Items;
        _count = state.Count;
    }

    private void Grow()
    {
        int newCapacity = _items.Length switch
        {
            0 => InitialCapacity,
            var length => length * 2
        };
        T[] newArray = ArrayPool<T>.Shared.Rent(newCapacity);
        _items.CopyTo(newArray);
        if (_pooledArray is not null)
        {
            if (ShouldResetItems)
            {
                _pooledArray.AsSpan().Clear();
            }
            ArrayPool<T>.Shared.Return(_pooledArray);
        }
        _items = _pooledArray = newArray;
    }

    public readonly int Count => _count;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public readonly Span<T> AllItems => _items[.._count];

    public void Push(T item)
    {
        if (_count == _items.Length)
        {
            Grow();
        }
        _items[_count++] = item;
    }

    public T Pop()
    {
        if (_count == 0)
        {
            ThrowHelpers.ThrowInvalidOperationException();
        }

        ref T resultRef = ref _items[_count - 1];
        _count--;
        T result = resultRef;
        if (ShouldResetItems)
        {
            resultRef = default!;
        }
        return result;
    }

    public void PopMany(int itemsToPop)
    {
        if ((uint)itemsToPop > (uint)_count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(itemsToPop));
        }

        if (ShouldResetItems)
        {
            _items[^itemsToPop..].Clear();
        }
        _count -= itemsToPop;
    }

    public void Clear()
    {
        if (_count == 0)
        {
            return;
        }
        if (ShouldResetItems)
        {
            _items[.._count].Clear();
        }
        _count = 0;
    }

    public void Dispose()
    {
        Clear();
        if (_pooledArray is not null)
        {
            ArrayPool<T>.Shared.Return(_pooledArray);
            _pooledArray = null;
        }
    }

    public readonly T Peek(int indexFromTheEnd = 0)
    {
        if ((uint)indexFromTheEnd >= (uint)_count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(indexFromTheEnd));
        }
        return _items[_count - 1 - indexFromTheEnd];
    }

    public readonly Span<T> PeekMany(int itemsToPeek)
    {
        if ((uint)itemsToPeek > (uint)_count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(itemsToPeek));
        }
        return _items.Slice(_count - itemsToPeek, itemsToPeek);
    }

    public State ExportState()
    {
        if (_pooledArray is null)
        {
            _pooledArray = ArrayPool<T>.Shared.Rent(_items.Length);
            _items.CopyTo(_pooledArray);
            _items = _pooledArray;
        }
        return new State(_pooledArray, _count);
    }

    public readonly struct State : IDisposable
    {
        public readonly T[] Items;
        public readonly int Count;

        internal State(T[] items, int count)
        {
            Items = items;
            Count = count;
        }

        public void Dispose()
        {
            if (Items is not null)
            {
                ArrayPool<T>.Shared.Return(Items);
            }
        }
    }
}
