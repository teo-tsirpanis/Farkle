// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle;

/// <summary>
/// Represents the position of a character in text.
/// </summary>
public readonly struct Position : IEquatable<Position>
#if NET6_0_OR_GREATER
    , ISpanFormattable
#endif
{
    private readonly int _line, _column;

    private Position(int line, int column)
    {
        _line = line;
        _column = column;
    }

    /// <summary>
    /// A <see cref="Position"/> that points to the start of text.
    /// </summary>
    public Position Initial => default;

    /// <summary>
    /// The line number of the <see cref="Position"/>.
    /// </summary>
    public int Line => _line + 1;
    /// <summary>
    /// The column number of the <see cref="Position"/>.
    /// </summary>
    public int Column => _column + 1;

    /// <summary>
    /// Creates a <see cref="Position"/> from zero-based coordinates.
    /// </summary>
    /// <param name="line">The line coordinate, starting from zero.</param>
    /// <param name="column">The column coordinate, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/>
    /// or <paramref name="column"/> are smaller than zero.</exception>
    public static Position Create0(int line, int column)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(line);
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(column);
        return new(line, column);
    }

    /// <summary>
    /// Creates a <see cref="Position"/> from one-based coordinates.
    /// </summary>
    /// <param name="line">The line coordinate, starting from one.</param>
    /// <param name="column">The column coordinate, starting from one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/>
    /// or <paramref name="column"/> are smaller than one.</exception>
    public static Position Create1(int line, int column) =>
        Create0(line - 1, column - 1);

    /// <summary>
    /// Checks two <see cref="Position"/>s for equality.
    /// </summary>
    /// <param name="other">The other position.</param>
    /// <returns>Whether <see langword="this"/> and <paramref name="other"/>
    /// have the same <see cref="Line"/> and <see cref="Column"/> values.</returns>
    public bool Equals(Position other) =>
        Line == other.Line && Column == other.Column;

    private string ToString(IFormatProvider? formatProvider) =>
#if NET6_0_OR_GREATER
        string.Create(formatProvider, stackalloc char[32], $"({Line}, {Column})");
#else
        formatProvider is null ? $"({Line}, {Column})" : ((FormattableString)$"({Line}, {Column})").ToString(formatProvider);
#endif

#if NET6_0_OR_GREATER
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        ToString(formatProvider);

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        destination.TryWrite(provider, $"({Line}, {Column})", out charsWritten);
#endif

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Position pos && Equals(pos);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Line, Column);

    /// <summary>
    /// Formats the <see cref="Position"/> to a string.
    /// </summary>
    /// <returns>The string <c>(<see cref="Line"/>, <see cref="Column"/>)</c></returns>
    public override string ToString() => ToString(null);
}
