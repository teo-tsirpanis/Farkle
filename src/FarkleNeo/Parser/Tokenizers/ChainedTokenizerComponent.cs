// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Represents a component of a chained tokenizer.
/// </summary>
/// <typeparam name="TChar">The type of characters the tokenizer reads.</typeparam>
public readonly struct ChainedTokenizerComponent<TChar>
{
    private ChainedTokenizerComponent(object? obj) => Value = obj;

    internal object? Value { get; private init; }

    /// <summary>
    /// A <see cref="ChainedTokenizerComponent{TChar}"/> that represents the default tokenizer.
    /// </summary>
    /// <remarks>
    /// When passing the builder to <see cref="CharParser{T}.WithTokenizerChain(ReadOnlySpan{ChainedTokenizerComponent{char}})"/>,
    /// the default tokenizer is the parser's existing tokenizer. Otherwise it is
    /// the tokenizer specified in <see cref="Tokenizer.CreateChain"/>, if provided.
    /// </remarks>
    public static ChainedTokenizerComponent<TChar> Default => default;

    /// <summary>
    /// Creates a <see cref="ChainedTokenizerComponent{TChar}"/> from a <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <param name="tokenizer">The tokenizer to use.</param>
    public static ChainedTokenizerComponent<TChar> Create(Tokenizer<TChar> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer, nameof(tokenizer));
        return new(tokenizer);
    }

    /// <summary>
    /// Creates a <see cref="ChainedTokenizerComponent{TChar}"/> from a tokenizer factory.
    /// </summary>
    /// <param name="tokenizerFactory">A delegate that creates a <see cref="Tokenizer{TChar}"/>
    /// from an <see cref="IGrammarProvider"/>.</param>
    public static ChainedTokenizerComponent<TChar> Create(Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizerFactory);
        return new(tokenizerFactory);
    }

    /// <summary>
    /// Implicit conversion operator from <see cref="Tokenizer{TChar}"/>
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <seealso cref="Create(Tokenizer{TChar})"/>
    public static implicit operator ChainedTokenizerComponent<TChar>(Tokenizer<TChar> tokenizer) =>
        Create(tokenizer);
}
