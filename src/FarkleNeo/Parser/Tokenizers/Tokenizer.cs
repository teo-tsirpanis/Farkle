// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Parser.Implementation;
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
    /// <para>
    /// To always support suspension, even standalone tokenizers are wrapped in a
    /// tokenizer chain, leading to an extra layer of indirection. By setting this
    /// property to <see langword="true"/>, Farkle does not wrap the tokenizer if
    /// it is the only one in the chain. This enables the tokenizer to be directly
    /// called by the parser, but the consequence is that suspending the tokenizer
    /// has no effect. It should therefore be used by tokenizers that are known to
    /// never suspend. An exception to this is when the tokenizer suspends by calling
    /// <see cref="TokenizerExtensions.SuspendTokenizer{TChar}(ref ParserInputReader{TChar}, Tokenizer{TChar})"/>
    /// with a resumption point of <see langword="this"/>.
    /// </para>
    /// <para>
    /// Additionally, tokenizers that set this property to <see langword="true"/> must
    /// handle thrown exceptions of type <sep cref="ParserApplicationException"/> and
    /// translate them to error <see cref="TokenizerResult"/>s, and must check the
    /// <see cref="ParserInputReader{TChar}.IsFinalBlock"/> property.
    /// </para>
    /// </remarks>
    internal bool CanSkipChainedTokenizerWrapping { get; private protected init; }

    /// <seealso cref="CharParser{T}.IsFailing"/>
    internal bool IsFailing { get; private protected init; }

    /// <summary>
    /// Tries to get the next token from the input.
    /// </summary>
    /// <param name="input">A <see cref="ParserInputReader{TChar}"/> with the input
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
    /// <see langword="false"/> in one of the following cases:
    /// <list type="bullet">
    /// <item><description>The tokenizer needs more input to make a decision.</description></item>
    /// <item><description>Input has ended and the <see cref="ParserInputReader{TChar}.IsFinalBlock"/>
    /// property of <paramref name="input"/> is <see langword="true"/>.</description></item>
    /// <item><description><strong>Not applicable for tokenizers that are wrapped in a chain:</strong>
    /// The tokenizer has encountered a noise symbol.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// A tokenizer object is considered to be wrapped in a chain if it was returned by
    /// <see cref="Tokenizer.CreateChain"/>, or by any other public API of Farkle.
    /// </para>
    /// </returns>
    public abstract bool TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result);
}

/// <summary>
/// Provides factory methods to create <see cref="Tokenizer{TChar}"/>s.
/// </summary>
public static class Tokenizer
{
    /// <summary>
    /// Creates a <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer accepts.</typeparam>
    /// <param name="grammar">The <see cref="Grammar"/> the tokenizer will use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grammar"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="TChar"/> is not <see cref="char"/>.</exception>
    /// <exception cref="InvalidOperationException">The grammar cannot be used for tokenizing.</exception>
    public static Tokenizer<TChar> Create<TChar>(Grammar grammar) => Create<TChar>(grammar, throwIfError: true);

    /// <summary>
    /// Creates a <see cref="Tokenizer{TChar}"/> from a tokenizer chain.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer accepts.</typeparam>
    /// <param name="components"></param>
    /// <param name="grammar">The <see cref="IGrammarProvider"/> to pass to the delegates given in
    /// <see cref="ChainedTokenizerComponent{TChar}.Create(Func{IGrammarProvider, Tokenizer{TChar}})"/>.
    /// This parameter is optional if no such delegates have been added.</param>
    /// <param name="defaultTokenizer">The tokenizer to use in place of
    /// <see cref="ChainedTokenizerComponent{TChar}.Default"/>.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"><paramref name="grammar"/> is <see langword="null"/>
    /// and <paramref name="components"/> contains a grammar-dependent tokenizer, or
    /// <paramref name="defaultTokenizer"/> is <see langwword="null"/> and
    /// <paramref name="components"/> contains the default tokenizer.</exception>
    public static Tokenizer<TChar> CreateChain<TChar>(ReadOnlySpan<ChainedTokenizerComponent<TChar>> components,
        IGrammarProvider? grammar = null, Tokenizer<TChar>? defaultTokenizer = null)
    {
        if (components.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(components), nameof(Resources.ChainedTokenizerBuilder_EmptyChain));
        }

        var builder = ImmutableArray.CreateBuilder<Tokenizer<TChar>>(components.Length);
        for (int i = 0; i < components.Length; i++)
        {
            switch (components[i].Value)
            {
                case Tokenizer<TChar> tokenizer:
                    builder.Add(tokenizer);
                    break;
                case Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory:
                    if (grammar is null)
                    {
                        ThrowHelpers.ThrowInvalidOperationException(Resources.ChainedTokenizerBuilder_NoGrammar);
                    }
                    builder.Add(tokenizerFactory(grammar));
                    break;
                case null:
                    if (defaultTokenizer is null)
                    {
                        ThrowHelpers.ThrowInvalidOperationException(Resources.ChainedTokenizerBuilder_NoDefaultTokenizer);
                    }
                    builder.Add(defaultTokenizer);
                    break;
                default:
                    ThrowHelpers.ThrowArgumentException($"{nameof(components)}[{i}]");
                    break;
            }
        }
        return ChainedTokenizer<TChar>.Create(builder.DrainToImmutable());
    }

    internal static Tokenizer<TChar> Create<TChar>(Grammar grammar, bool throwIfError, object? customError = null)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(grammar);
        if (grammar.IsUnparsable(out string? errorKey))
        {
            return Fail(errorKey);
        }
        if (grammar.GetDfa<TChar>() is not { } dfa)
        {
            return Fail(nameof(Resources.Parser_GrammarDfaMissing));
        }
        if (dfa.HasConflicts || dfa[0].AcceptSymbols.Count > 0)
        {
            return Fail(nameof(Resources.Parser_GrammarDfaProblem));
        }
        return ChainedTokenizer<TChar>.Create(new DefaultTokenizer<TChar>(grammar, dfa));

        Tokenizer<TChar> Fail(string resourceKey) =>
            throwIfError
            ? throw new InvalidOperationException(Resources.GetResourceString(resourceKey))
            : new FailingTokenizer<TChar>(customError ?? LocalizedDiagnostic.Create(resourceKey));
    }
}
