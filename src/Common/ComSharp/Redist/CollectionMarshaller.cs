// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ComSharp;

[Embedded]
internal static class CollectionMarshaller
{
    [return: NotNullIfNotNull(nameof(xs))]
    public static IEnumerable<TOut>? Marshal<TIn, TOut>(IEnumerable<TIn>? xs, Func<TIn, TOut> f) =>
        xs is null ? null : new Enumerable<TIn, TOut>(xs, f);

    [return: NotNullIfNotNull(nameof(xs))]
    public static IReadOnlyList<TOut>? Marshal<TIn, TOut>(IReadOnlyList<TIn>? xs, Func<TIn, TOut> f) =>
        xs is null ? null : new ReadOnlyList<TIn, TOut>(xs, f);

    private class ReadOnlyList<TIn, TOut>(IReadOnlyList<TIn> xs, Func<TIn, TOut> f) : Enumerable<TIn, TOut>(xs, f), IReadOnlyList<TOut>
    {
        public TOut this[int index] => F(xs[index]);

        public int Count => xs.Count;
    }

    private class Enumerable<TIn, TOut>(IEnumerable<TIn> xs, Func<TIn, TOut> f) : IEnumerable<TOut>
    {
        protected readonly Func<TIn, TOut> F = f;

        public IEnumerator<TOut> GetEnumerator() => new Enumerator<TIn, TOut>(xs.GetEnumerator(), F);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Enumerator<TIn, TOut>(IEnumerator<TIn> x, Func<TIn, TOut> f) : IEnumerator<TOut>
    {
        public TOut Current => f(x.Current);

        object IEnumerator.Current => Current!;

        public void Dispose() => x.Dispose();

        public bool MoveNext() => x.MoveNext();

        public void Reset() => x.Reset();
    }
}
