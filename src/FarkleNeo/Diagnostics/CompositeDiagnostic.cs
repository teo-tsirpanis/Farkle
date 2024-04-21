// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Text;

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a collection of diagnostic objects and supports formatting them
/// as a line-delimited string.
/// </summary>
internal sealed class CompositeDiagnostic<T>(ImmutableArray<T> diagnostics) : IReadOnlyList<T>, IFormattable
{
    public ImmutableArray<T> Diagnostics { get; } = diagnostics;

    public int Count => Diagnostics.Length;

    public T this[int index] => Diagnostics[index];

    public CompositeDiagnostic(IEnumerable<T> diagnostics) : this(diagnostics.ToImmutableArray()) { }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Diagnostics).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var sb = new StringBuilder();
        foreach (T diagnostic in Diagnostics)
        {
#if NET6_0_OR_GREATER
            sb.AppendLine(formatProvider, $"{diagnostic}");
#else
            sb.AppendFormat(formatProvider, "{0}", diagnostic).AppendLine();
#endif
        }
        return sb.ToString();
    }

    public override string ToString() => ToString(null, null);
}
