// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using System.Collections.Immutable;

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a syntax error.
/// </summary>
/// <remarks>
/// A syntax error occurs when the parser encounters a token in an unexpected place.
/// </remarks>
public sealed class SyntaxError : IFormattable
{
    /// <summary>
    /// The name of the token found by the parser, or <see langword="null"/> if the end of the input was reached.
    /// </summary>
    /// <remarks>
    /// A value of <see langword="null"/> indicates that the parser encountered the end of the input.
    /// </remarks>
    public string? ActualTokenName { get; }

    /// <summary>
    /// The names of the tokens that the parser expected to find.
    /// </summary>
    /// <remarks>
    /// A value of <see langword="null"/> in the array indicates that the parser also expected the end of the input.
    /// </remarks>
    public ImmutableArray<string?> ExpectedTokenNames { get; }

    /// <summary>
    /// The state the parser's state machine was at the time of the error.
    /// </summary>
    public int ParserState { get; }

    /// <summary>
    /// Creates a <see cref="SyntaxError"/>.
    /// </summary>
    /// <param name="actualTokenName">The value of <see cref="ActualTokenName"/>.</param>
    /// <param name="expectedTokenNames">The value of <see cref="ExpectedTokenNames"/>.</param>
    /// <param name="parserState">The value of <see cref="ParserState"/>.
    /// Optional, defaults to -1.</param>
    public SyntaxError(string? actualTokenName, ImmutableArray<string?> expectedTokenNames, int parserState = -1)
    {
        if (expectedTokenNames.IsDefault)
        {
            expectedTokenNames = ImmutableArray<string?>.Empty;
        }
        ActualTokenName = actualTokenName;
        ExpectedTokenNames = expectedTokenNames;
        ParserState = parserState;
    }

    private string ToString(IFormatProvider? formatProvider)
    {
        string eofString = Resources.GetEofString(formatProvider);
        return Resources.Format(formatProvider,
            nameof(Resources.Parser_UnexpectedToken),
            ActualTokenName ?? eofString,
            new DelimitedString(ExpectedTokenNames, ", ", eofString, TokenSymbol.FormatName));
    }

    string IFormattable.ToString(string format, IFormatProvider formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
