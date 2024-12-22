// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Represents a <see cref="Tokenizer{TChar}"/>'s successful or failed result.
/// </summary>
/// <remarks>
/// Besides success and failure, the third possible outcome happens when the
/// tokenizer needs more input to make a decision. In this case the
/// <see cref="Tokenizer{TChar}.TryGetNextToken"/> method will return
/// <see langword="false"/>.
/// </remarks>
public readonly struct TokenizerResult
{
    /// <summary>
    /// Whether the <see cref="TokenizerResult"/> represents success.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Data))]
    public bool IsSuccess => Symbol.HasValue;

    /// <summary>
    /// The <see cref="TokenSymbolHandle"/> that identifies the symbol of token.
    /// </summary>
    /// <remarks>
    /// If <see cref="IsSuccess"/> is <see langword="false"/>, this property
    /// will have a value of <see langword="default"/>.
    /// </remarks>
    public TokenSymbolHandle Symbol { get; private init; }

    /// <summary>
    /// The token's semantic value in case of success or an object describing
    /// the error in case of failure. In the latter case the value is not
    /// <see langword="null"/>.
    /// </summary>
    public object? Data { get; private init; }

    /// <summary>
    /// Contains the position of the first character of the token.
    /// </summary>
    public TextPosition Position { get; private init; }

    /// <summary>
    /// Creates a <see cref="TokenizerResult"/> signifying success.
    /// </summary>
    /// <param name="symbol">The token's symbol.</param>
    /// <param name="data">The token's semantic value.</param>
    /// <param name="position">The token's position.</param>
    public static TokenizerResult CreateSuccess(TokenSymbolHandle symbol, object? data, TextPosition position)
    {
        if (!symbol.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(symbol));
        }
        return new TokenizerResult() { Symbol = symbol, Data = data, Position = position };
    }

    /// <summary>
    /// Creates a <see cref="TokenizerResult"/> signifying success.
    /// </summary>
    /// <param name="error">An object describing the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static TokenizerResult CreateError(object error)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(error);
        return new TokenizerResult() { Data = error };
    }
}
