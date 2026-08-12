// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;

namespace Farkle.Analyzers.Models;

public readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> _array = array;

    public T this[int index] => _array[index];

    public int Count => _array.Length;

    public bool Equals(EquatableArray<T> other) => _array.SequenceEqual(other._array);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public ImmutableArray<T> ToImmutableArray() => _array;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_array.Length);
        foreach (var item in _array)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => _array.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_array).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_array).GetEnumerator();
}

public static class EquatableArray
{
    extension<T>(ImmutableArray<T>.Builder builder)
    {
        public EquatableArray<T> DrainToEquatable() => new(builder.DrainToImmutable());
    }

    extension<T>(IEnumerable<T> items)
    {
        public EquatableArray<T> ToEquatableArray() => new(items.ToImmutableArray());
    }
}
