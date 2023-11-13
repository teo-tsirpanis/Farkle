// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a lexical error.
/// </summary>
/// <remarks>
/// A lexical error occurs when the tokenizer cannot recognize some characters as part of a token.
/// </remarks>
/// <param name="tokenText">The value of <see cref="TokenText"/>.</param>
/// <param name="tokenizerState">The value of <see cref="TokenizerState"/>.
/// Optional, defaults to -1.</param>
public sealed class LexicalError(string? tokenText, int tokenizerState = -1) : IFormattable
#if NET8_0_OR_GREATER
    , ISpanFormattable
#endif
{
    /// <summary>
    /// The characters of the token that caused the error.
    /// </summary>
    /// <remarks>
    /// This value might be truncated by Farkle if the token is too long or spans multiple lines.
    /// </remarks>
    public string? TokenText { get; } = tokenText;

    /// <summary>
    /// The state the tokenizer's state machine was at the time of the error.
    /// </summary>
    public int TokenizerState { get; } = tokenizerState;

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Parser_UnrecognizedToken), TokenText);

#if NET8_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Resources.TryWrite(destination, provider, nameof(Resources.Parser_UnrecognizedToken), out charsWritten, TokenText);
#endif

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
