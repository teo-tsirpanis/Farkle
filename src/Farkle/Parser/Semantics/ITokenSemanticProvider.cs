// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Parser.Semantics;

/// <summary>
/// Provides an interface to run semantic actions on a token.
/// </summary>
/// <typeparam name="TChar">The type of characters the token is made of.</typeparam>
/// <seealso cref="ISemanticProvider{TChar, T}"/>
public interface ITokenSemanticProvider<TChar>
{
    /// <summary>
    /// Converts the characters of a token to a semantic value. This method is
    /// called by the tokenizer when a token corresponding to a terminal is found.
    /// </summary>
    /// <param name="parserState">The state of the parsing operation.</param>
    /// <param name="symbol">The symbol of the token that was found.</param>
    /// <param name="characters">The characters of the token.</param>
    /// <returns>The semantic value of the token.</returns>
    public object? Transform(ref ParserState parserState, TokenSymbolHandle symbol, ReadOnlySpan<TChar> characters);
}
