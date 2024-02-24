// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Grammars;

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a lexical error.
/// </summary>
/// <remarks>
/// A lexical error occurs when the tokenizer cannot recognize some characters as part of a token.
/// </remarks>
public sealed class LexicalError : IFormattable, IParserStateInfoSupplier
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
    public string? TokenText { get; }

    /// <summary>
    /// The state the tokenizer's state machine was at the time of the error.
    /// </summary>
    public int TokenizerState { get; }

    /// <inheritdoc cref="SyntaxError.ExpectedTokenNames"/>
    public ImmutableArray<string?> ExpectedTokenNames { get; }

    /// <inheritdoc cref="SyntaxError.ParserState"/>
    public int ParserState { get; }

    private LexicalError(string? tokenText, int tokenizerState, ImmutableArray<string?> expectedTokenNames, int parserState)
    {
        TokenText = tokenText;
        TokenizerState = tokenizerState;
        ExpectedTokenNames = expectedTokenNames;
        ParserState = parserState;
    }

    /// <summary>
    /// Creates a <see cref="LexicalError"/>.
    /// </summary>
    /// <param name="tokenText">The value of <see cref="TokenText"/>.</param>
    /// <param name="tokenizerState">The value of <see cref="TokenizerState"/>.
    /// Optional, defaults to -1.</param>
    public LexicalError(string? tokenText, int tokenizerState = -1) : this(tokenText, tokenizerState, [], -1) { }

    object IParserStateInfoSupplier.WithParserStateInfo(ImmutableArray<string?> expectedTokenNames, int parserState) =>
        new LexicalError(TokenText, TokenizerState, expectedTokenNames, parserState);

    private string ToString(IFormatProvider? formatProvider)
    {
        string eofString = Resources.GetEofString(formatProvider);
        return Resources.Format(formatProvider,
            nameof(Resources.Parser_UnrecognizedToken),
            TokenText,
            new DelimitedString(ExpectedTokenNames, ", ", eofString, TokenSymbol.FormatName));
    }

#if NET8_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        string eofString = Resources.GetEofString(provider);
        return Resources.TryWrite(destination, provider,
            nameof(Resources.Parser_UnrecognizedToken), out charsWritten,
            TokenText,
            new DelimitedString(ExpectedTokenNames, ", ", eofString, TokenSymbol.FormatName));
    }
#endif

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
