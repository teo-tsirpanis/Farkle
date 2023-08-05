// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser.Semantics;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Provides an interface to break a sequence of characters into tokens.
/// </summary>
/// <typeparam name="TChar">The type of characters the tokens are made of.</typeparam>
public abstract class Tokenizer<TChar>
{
    /// <summary>
    /// Creates a <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    protected Tokenizer() { }

    /// <summary>
    /// Whether the tokenizer does not need to be wrapped in a tokenizer chain.
    /// </summary>
    /// <remarks>
    /// To always support suspension, even standalone tokenizers are wrapped in a
    /// tokenizer chain, leading to an extra layer of indirection. By setting this
    /// property to <see langword="true"/>, Farkle does not wrap the tokenizer if
    /// it is the only one in the chain. This enables the tokenizer to be directly
    /// called by the parser, but the consequence is that suspending the tokenizer
    /// has no effect. It should therefore be used by tokenizers that are known to
    /// never suspend.
    /// </remarks>
    internal bool CanSkipChainedTokenizerWrapping { get; private protected init; }

    /// <seealso cref="CharParser{T}.IsFailing"/>
    internal bool IsFailing { get; private protected init; }

    /// <summary>
    /// Tries to get the next token from the input.
    /// </summary>
    /// <param name="reader">A <see cref="ParserInputReader{TChar}"/> with the input
    /// and the <see cref="ParserState"/>.</param>
    /// <param name="semanticProvider">An <see cref="ITokenSemanticProvider{TChar}"/>
    /// to create the semantic values for the tokens.</param>
    /// <param name="result">Will hold the <see cref="TokenizerResult"/> if the method
    /// returns <see langword="true"/>.</param>
    /// <returns>
    /// <para>
    /// <see langword="true"/> if the tokenizer has found a token or failed with an error.
    /// In this case the result will be written to <paramref name="result"/>.
    /// </para>
    /// <para>
    /// <see langword="false"/> if either the tokenizer needs more input to make a decision
    /// or input has ended if the <see cref="ParserInputReader{TChar}.IsFinalBlock"/> property
    /// of <paramref name="reader"/> is <see langword="true"/>. In this case the tokenizer
    /// should be invoked again after more input has been provided.
    /// </para>
    /// </returns>
    public abstract bool TryGetNextToken(ref ParserInputReader<TChar> reader, ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result);
}
