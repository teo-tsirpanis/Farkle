// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message from the parser.
/// </summary>
public sealed class ParserDiagnostic : ISpanFormattable
{
    /// <summary>
    /// The position the message was reported at.
    /// </summary>
    public TextPosition Position { get; }

    /// <summary>
    /// An <see cref="object"/> that describes the message.
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// Creates a <see cref="ParserDiagnostic"/>.
    /// </summary>
    /// <param name="position">The value of <see cref="Position"/>.</param>
    /// <param name="message">The value of <see cref="Message"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>
    /// is <see langword="null"/>.</exception>
    public ParserDiagnostic(TextPosition position, object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Position = position;
        Message = message;
    }

    private string ToString(IFormatProvider? formatProvider) =>
        string.Create(formatProvider, $"{Position} {Message}");

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        destination.TryWrite(provider, $"{Position} {Message}", out charsWritten);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
