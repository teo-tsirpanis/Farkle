// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Farkle.Collections;

/// <summary>
/// Represents a collection that can be either an <see cref="ImmutableArray{T}"/> or an
/// <see cref="ImmutableList{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
/// <remarks>
/// This struct is optimized both for the case where the collection is initialized
/// with a known list of elements, and when it is built incrementally. In the former
/// case, converting it to an <see cref="ImmutableArray{T}"/> does not allocate.
/// </remarks>
[CollectionBuilder(typeof(ImmutableArrayOrList), nameof(ImmutableArrayOrList.Create))]
internal readonly struct ImmutableArrayOrList<T> : IReadOnlyCollection<T>
{
    private readonly IReadOnlyCollection<T> _value;

    public static ImmutableArrayOrList<T> Empty => new(ImmutableArray<T>.Empty);

    public ImmutableArrayOrList(ImmutableArray<T> array)
    {
        Debug.Assert(!array.IsDefault);
        _value = ImmutableCollectionsMarshal.AsArray(array)!;
    }

    public ImmutableArrayOrList(ImmutableList<T> list)
    {
        _value = list;
    }

    public ImmutableArrayOrList<T> Add(T item) => new(ToImmutableList().Add(item));

    public int Count => _value.Count;

    public IEnumerator<T> GetEnumerator() => _value.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ImmutableList<T> ToImmutableList() => _value is ImmutableList<T> list
        ? list
        : ImmutableList.CreateRange(_value);

    public ImmutableArray<T> ToImmutableArray() => _value is T[] array
        ? ImmutableCollectionsMarshal.AsImmutableArray(array)
        : ImmutableArray.CreateRange(_value);
}

internal static class ImmutableArrayOrList
{
    public static ImmutableArrayOrList<T> Create<T>(ReadOnlySpan<T> span) => new(span.ToImmutableArray());
}
