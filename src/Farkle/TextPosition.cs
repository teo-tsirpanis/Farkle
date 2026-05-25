// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle;

/// <summary>
/// Represents the position of a character in text.
/// </summary>
public readonly struct TextPosition : IEquatable<TextPosition>, ISpanFormattable
{
    private readonly int _line, _column;

    private TextPosition(int line, int column)
    {
        _line = line;
        _column = column;
    }

    /// <summary>
    /// A <see cref="TextPosition"/> that points to the start of text.
    /// </summary>
    public static TextPosition Initial => default;

    /// <summary>
    /// The line number of the <see cref="TextPosition"/>, starting from 1.
    /// </summary>
    public int Line => _line + 1;
    /// <summary>
    /// The column number of the <see cref="TextPosition"/>, starting from 1.
    /// </summary>
    public int Column => _column + 1;

    /// <summary>
    /// Creates a <see cref="TextPosition"/> from zero-based coordinates.
    /// </summary>
    /// <param name="line">The line coordinate, starting from zero.</param>
    /// <param name="column">The column coordinate, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/>
    /// or <paramref name="column"/> are smaller than zero.</exception>
    public static TextPosition Create0(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        return new(line, column);
    }

    /// <summary>
    /// Creates a <see cref="TextPosition"/> from one-based coordinates.
    /// </summary>
    /// <param name="line">The line coordinate, starting from one.</param>
    /// <param name="column">The column coordinate, starting from one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/>
    /// or <paramref name="column"/> are smaller than one.</exception>
    public static TextPosition Create1(int line, int column) =>
        Create0(line - 1, column - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TextPosition AdvanceCore<T>(ReadOnlySpan<T> span, T cr, T lf)
        where T : struct, IEquatable<T>
    {
        // Fast path: most tokens (identifiers, keywords, punctuation, numbers)
        // contain no newlines. Avoid the loop and Create0 validation overhead.
        int nlPos = span.IndexOfAny(lf, cr);
        if (nlPos < 0)
        {
            return new(_line, _column + span.Length);
        }
        return AdvanceCoreSlow(span, cr, lf, nlPos);
    }

    private TextPosition AdvanceCoreSlow<T>(ReadOnlySpan<T> span, T cr, T lf, int nlPos)
        where T : struct, IEquatable<T>
    {
        // We advance the line number if:
        // 1. We found a CR and it is not the last character in the span.
        // 2. We found an LF.
        // 3. We found a CRLF sequence.
        int line = _line, column = _column;
        do
        {
            // CR or LF found.
            bool foundCr = span[nlPos].Equals(cr);
            // If the character is a CR, and it is the last character in the span,
            // advance the column number by the number of characters before it.
            if (foundCr && nlPos == span.Length - 1)
            {
                column += nlPos;
            }
            // Otherwise (LF or CR not at the end of the span), advance the line number.
            else
            {
                line++;
                column = 0;
            }
            // We will continue searching from the character after the CR or LF we found above.
            int nextChar = nlPos + 1;
            // But, if the character was a CR, it was not the last character in the span,
            // and the character after it is an LF, we will skip the LF; we have already
            // advanced the line number for the CRLF sequence.
            if (foundCr && nextChar < span.Length && span[nextChar].Equals(lf))
            {
                nextChar++;
            }
            // Slice the span to the remaining characters.
            span = span[nextChar..];
            nlPos = span.IndexOfAny(lf, cr);
        }
        while (nlPos >= 0);

        // No more CR or LF found. Advance the column number by the remaining
        // characters and return.
        return new(line, column + span.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TextPosition Advance<T>(ReadOnlySpan<T> span)
    {
        // Because of lack of language support, we have to do some duplication here.
        if (typeof(T) == typeof(char))
        {
            return AdvanceCore(Utilities.BitCastSpan<T, char>(span), '\r', '\n');
        }
        if (typeof(T) == typeof(byte))
        {
            return AdvanceCore(Utilities.BitCastSpan<T, byte>(span), (byte)'\r', (byte)'\n');
        }
        // For any other type, we will just advance the column number by the length of the span.
        return new(_line, _column + span.Length);
    }

    internal TextPosition NextLine() => new(_line + 1, 0);

    private string ToString(IFormatProvider? formatProvider) =>
        string.Create(formatProvider, stackalloc char[32], $"({Line}, {Column})");

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        ToString(formatProvider);

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        destination.TryWrite(provider, $"({Line}, {Column})", out charsWritten);

    /// <summary>
    /// Checks two <see cref="TextPosition"/>s for equality.
    /// </summary>
    /// <param name="other">The other position.</param>
    /// <returns>Whether <see langword="this"/> and <paramref name="other"/>
    /// have the same <see cref="Line"/> and <see cref="Column"/> values.</returns>
    public bool Equals(TextPosition other) =>
        _line == other._line && _column == other._column;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is TextPosition pos && Equals(pos);

    /// <summary>
    /// Implements the equality operator for <see cref="TextPosition"/>.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns>Whether the two positions are equal.</returns>
    public static bool operator ==(TextPosition left, TextPosition right) => left.Equals(right);

    /// <summary>
    /// Implements the inequality operator for <see cref="TextPosition"/>.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns>Whether the two positions are not equal.</returns>
    public static bool operator !=(TextPosition left, TextPosition right) => !left.Equals(right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Line, Column);

    /// <summary>
    /// Formats the <see cref="TextPosition"/> to a string.
    /// </summary>
    /// <returns>The string <c>(<see cref="Line"/>, <see cref="Column"/>)</c></returns>
    public override string ToString() => ToString(null);
}
