// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a lexical error.
/// </summary>
/// <remarks>
/// A lexical error occurs when the tokenizer cannot recognize some characters as part of a token.
/// </remarks>
public sealed class LexicalError : IFormattable
{
    /// <summary>
    /// The characters of the token that caused the error.
    /// </summary>
    /// <remarks>
    /// This value might be truncated by Farkle if the token is too long or spans multiple lines.
    /// </remarks>
    public string? TokenText { get; }

    /// <summary>
    /// The number of the tokenizer's state machine at the time of the error.
    /// </summary>
    public int TokenizerState { get; }

    /// <summary>
    /// Creates a <see cref="LexicalError"/>.
    /// </summary>
    /// <param name="tokenText">The value of <see cref="TokenText"/>.</param>
    /// <param name="tokenizerState">The value of <see cref="TokenizerState"/>.
    /// Optional, defaults to -1.</param>
    public LexicalError(string? tokenText, int tokenizerState = -1)
    {
        TokenText = tokenText;
        TokenizerState = tokenizerState;
    }

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Parser_UnrecognizedToken), TokenText);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
