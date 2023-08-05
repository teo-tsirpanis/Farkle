// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser.Semantics;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Provides a way for a <see cref="Tokenizer{TChar}"/> to resume at multiple
/// points after suspending.
/// </summary>
/// <typeparam name="TChar">The type of characters the tokenizer reads.</typeparam>
/// <typeparam name="TArg">A type a value of which will be passed to the resumption point.
/// Implementing this interface with multiple <typeparamref name="TArg"/> types
/// allows defining many suspension points.</typeparam>
/// <seealso cref="TokenizerExtensions.SuspendTokenizer{TChar, TArg}(ref ParserState, ITokenizerResumptionPoint{TChar, TArg}, TArg)"/>
public interface ITokenizerResumptionPoint<TChar, in TArg>
{
    /// <summary>
    /// Tries to get the next token from the input.
    /// This method is identical to <see cref="Tokenizer{TChar}.TryGetNextToken"/>
    /// with the only addition of the <paramref name="arg"/> parameter.
    /// </summary>
    /// <param name="reader">A <see cref="ParserInputReader{TChar}"/> with the input
    /// and the <see cref="ParserState"/>.</param>
    /// <param name="semanticProvider">An <see cref="ITokenSemanticProvider{TChar}"/>
    /// to create the semantic values for the tokens.</param>
    /// <param name="arg">The value that had been passed in
    /// <see cref="TokenizerExtensions.SuspendTokenizer{TChar, TArg}(ref ParserState, ITokenizerResumptionPoint{TChar, TArg}, TArg)"/>.</param>
    /// <param name="result">Will hold the <see cref="TokenizerResult"/> if the method
    /// returns <see langword="true"/>.</param>
    /// <returns>Whether the tokenizer was given enough characters
    /// to either find a token or conclusively fail. See
    /// <see cref="Tokenizer{TChar}.TryGetNextToken"/> for more details about the semantics of
    /// the method's return type.</returns>
    public bool TryGetNextToken(ref ParserInputReader<TChar> reader,
        ITokenSemanticProvider<TChar> semanticProvider, TArg arg, out TokenizerResult result);
}
