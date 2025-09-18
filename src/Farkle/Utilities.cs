// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle;

internal static class Utilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T UnsafeCast<T>(object o) where T : class
    {
        Debug.Assert(o is T);
        return Unsafe.As<T>(o);
    }

    /// <summary>
    /// Converts an array to a span, while throwing if it is null.
    /// </summary>
    public static Span<T> AsSpanChecked<T>([NotNull] this T[]? array,
        [CallerArgumentExpression((nameof(array)))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(array, paramName);
        return array.AsSpan();
    }

    /// <summary>
    /// Converts an array to an immutable array, while throwing if it is null.
    /// </summary>
    public static ImmutableArray<T> ToImmutableArrayChecked<T>([NotNull] this T[]? array,
        [CallerArgumentExpression((nameof(array)))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(array, paramName);
        return ImmutableArray.Create(array);
    }

    /// <summary>
    /// Gets an equality comparer that compares two strings by content, and
    /// other objects by <see cref="object.Equals(object?, object?)"/>.
    /// </summary>
    /// <param name="caseSensitive">Whether to compare strings in a case-sensitive way.</param>
    public static IEqualityComparer<object> GetFallbackStringComparer(bool caseSensitive) =>
        caseSensitive ? FallbackStringComparer.CaseSensitive : FallbackStringComparer.CaseInsensitive;

    private sealed class FallbackStringComparer(bool caseSensitive) : IEqualityComparer<object>
    {
        public static readonly IEqualityComparer<object> CaseSensitive = new FallbackStringComparer(true);
        public static readonly IEqualityComparer<object> CaseInsensitive = new FallbackStringComparer(false);

        private readonly StringComparer _stringComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        public new bool Equals(object? x, object? y)
        {
            if (x is string xString && y is string yString)
            {
                return _stringComparer.Equals(xString, yString);
            }
            return object.Equals(x, y);
        }

        public int GetHashCode(object obj) => obj is string str
            ? 2 * _stringComparer.GetHashCode(str)
            : 2 * obj.GetHashCode() + 1;
    }
}
