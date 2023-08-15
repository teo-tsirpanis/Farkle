// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message from the parser.
/// </summary>
public sealed class ParserDiagnostic : IFormattable
#if NET6_0_OR_GREATER
    , ISpanFormattable
#endif
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
        ArgumentNullExceptionCompat.ThrowIfNull(message);
        Position = position;
        Message = message;
    }

    private string ToString(IFormatProvider? formatProvider) =>
#if NET6_0_OR_GREATER
        string.Create(formatProvider, $"{Position} {Message}");
#else
        ((FormattableString)$"{Position} {Message}").ToString(formatProvider);
#endif

#if NET6_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        destination.TryWrite(provider, $"{Position} {Message}", out charsWritten);
#endif

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
