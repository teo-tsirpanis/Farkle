// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Farkle.Buffers;

/// <summary>
/// Represents an immutable memory buffer.
/// </summary>
/// <typeparam name="T">The type of elements the buffer stores.</typeparam>
/// <remarks>
/// This is an enhanced version of <see cref="ImmutableArray{T}"/>, which supports
/// wrapping <see cref="string"/>s in addition to arrays, if <typeparamref name="T"/>
/// is <see cref="char"/>.
/// </remarks>
[CollectionBuilder(typeof(ImmutableBuffer), nameof(ImmutableBuffer.Create))]
internal readonly struct ImmutableBuffer<T> : IEnumerable<T>
{
    public object? RawValue { get; }

    public ImmutableBuffer(object? rawValue)
    {
        Debug.Assert(rawValue is null or T[] || (typeof(T) == typeof(char) && rawValue is string));
        RawValue = rawValue;
    }

    public static ImmutableBuffer<T> Empty => default;

    public ReadOnlySpan<T> Span
    {
        get
        {
            if (typeof(T) == typeof(char) && RawValue is string s)
            {
                return Utilities.BitCastSpan<char, T>(s);
            }
            return (T[]?)RawValue;
        }
    }

    public static implicit operator ImmutableBuffer<T>(ImmutableArray<T> array) =>
        ImmutableBuffer.Create(array);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>?)RawValue ?? []).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsEmpty => Span.IsEmpty;

    public int Length => Span.Length;
}

internal static class ImmutableBuffer
{
    public static ImmutableBuffer<T> Create<T>(ImmutableArray<T> array) => new(ImmutableCollectionsMarshal.AsArray(array));

    public static ImmutableBuffer<T> Create<T>(IEnumerable<T> enumerable)
    {
        if (typeof(T) == typeof(char) && enumerable is string s)
        {
            return new(s);
        }
        if (enumerable is ImmutableArray<T> array)
        {
            return Create(array);
        }
        return new(enumerable.ToArray());
    }

    public static ImmutableBuffer<char> Create(string? s) => new(s);

    public static ImmutableBuffer<T> Create<T>(ReadOnlySpan<T> span) => new(span.ToArray());
}
