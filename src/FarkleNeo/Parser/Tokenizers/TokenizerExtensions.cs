// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser.Semantics;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Provides extension methods on <see cref="ParserState"/> specific to tokenizers.
/// </summary>
/// <remarks>
/// Calling these methods outside of a tokenizer is undefined behavior.
/// </remarks>
public static class TokenizerExtensions
{
    internal static ChainedTokenizerState<TChar>? GetChainedTokenizerStateOrNull<TChar>(this in ParserInputReader<TChar> input)
    {
        if (input.State.TryGetValue(typeof(ChainedTokenizerState<TChar>), out object? value))
        {
            Debug.Assert(value is ChainedTokenizerState<TChar>);
            return Unsafe.As<ChainedTokenizerState<TChar>>(value);
        }
        return null;
    }

    internal static ChainedTokenizerState<TChar> GetOrCreateChainedTokenizerState<TChar>(this ref ParserInputReader<TChar> input)
    {
        if (!input.State.TryGetValue(typeof(ChainedTokenizerState<TChar>), out object? value))
        {
            value = new ChainedTokenizerState<TChar>();
            input.State.SetValue(typeof(ChainedTokenizerState<TChar>), value);
        }
        Debug.Assert(value is ChainedTokenizerState<TChar>);
        return Unsafe.As<ChainedTokenizerState<TChar>>(value);
    }

    private static void SuspendTokenizerCore<TChar>(this ref ParserInputReader<TChar> input, Tokenizer<TChar> tokenizer)
    {
        var tokenizerState = input.GetOrCreateChainedTokenizerState();
        if (tokenizerState.TokenizerToResume is not null)
        {
            ThrowHelpers.ThrowInvalidOperationException(Resources.Tokenizer_AlreadySuspended);
        }
        tokenizerState.TokenizerToResume = tokenizer;
    }

    /// <summary>
    /// Returns whether the tokenizer chain in a parsing operation consists of only one tokenizer.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer processes.</typeparam>
    /// <param name="input">The state of the parsing operation.</param>
    /// <remarks>
    /// If this property is <see langword="true"/>, tokenizers can avoid exiting when they
    /// encounter a noise symbol, and instead continue tokenizing. Checking for this property
    /// is optional and provides a performance improvement.
    /// </remarks>
    public static bool IsSingleTokenizerInChain<TChar>(this in ParserInputReader<TChar> input) =>
        input.State.IsSingleTokenizerInChain;

    /// <summary>
    /// Suspends the tokenization process and sets it to resume at the specified <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer processes.</typeparam>
    /// <param name="input">The state of the parsing operation.</param>
    /// <param name="tokenizer">The tokenizer to resume at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tokenizer is already suspended.</exception>
    /// <remarks>
    /// <para>
    /// This method is intended to be called by tokenizers that want to keep running when they return.
    /// The reasons they want that can be to ask for more characters or to produce many tokens consecutively.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// If the tokenizer is part of a chain and does not return a result, subsequent components
    /// of the chain will not be invoked. Instead the parent tokenizer will return without a result,
    /// causing the parser to return and request more input.
    /// </item>
    /// <item>
    /// When the parent tokenizer gets invoked again, it will first invoke <paramref name="tokenizer"/>
    /// and if needed, continue the chain at the tokenizer after the tokenizer that initially suspended.
    /// </item>
    /// </list>
    /// <para>
    /// Because suspending tokenizers more than once is not allowed, tokenizers should return right after
    /// they call this method.
    /// </para>
    /// </remarks>
    public static void SuspendTokenizer<TChar>(this ref ParserInputReader<TChar> input, Tokenizer<TChar> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer);

        if (!input.State.TokenizerSupportsSuspending)
        {
            return;
        }
        input.SuspendTokenizerCore(tokenizer);
    }

    /// <summary>
    /// Suspends the tokenization process and sets it to resume at the specified
    /// <see cref="ITokenizerResumptionPoint{TChar, TArg}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer processes.</typeparam>
    /// <typeparam name="TArg">The type of <paramref name="arg"/>.</typeparam>
    /// <param name="input">The state of the parsing operation.</param>
    /// <param name="resumptionPoint">The resumption point to resume at.</param>
    /// <param name="arg">An argument to pass to <paramref name="resumptionPoint"/>
    /// when it gets invoked.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resumptionPoint"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tokenizer is already suspended.</exception>
    /// <remarks>
    /// <para>
    /// This method is intended to be called by tokenizers that want to keep running when they return.
    /// The reasons they want that can be to ask for more characters or to produce many tokens consecutively.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// If the tokenizer is part of a chain and does not return a result, subsequent components
    /// of the chain will not be invoked. Instead the parent tokenizer will return without a result,
    /// causing the parser to return and request more input.
    /// </item>
    /// <item>
    /// When the parent tokenizer gets invoked again, it will first invoke <paramref name="resumptionPoint"/>
    /// and if needed, continue the chain at the tokenizer after the tokenizer that initially suspended.
    /// </item>
    /// </list>
    /// <para>
    /// Because suspending tokenizers more than once is not allowed, tokenizers should return right after
    /// they call this method.
    /// </para>
    /// </remarks>
    public static void SuspendTokenizer<TChar, TArg>(this ref ParserInputReader<TChar> input,
        ITokenizerResumptionPoint<TChar, TArg> resumptionPoint, TArg arg)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(resumptionPoint);

        if (!input.State.TokenizerSupportsSuspending)
        {
            return;
        }
        input.SuspendTokenizerCore(new TokenizerResumptionPoint<TChar, TArg>(resumptionPoint, arg));
    }

    private sealed class TokenizerResumptionPoint<TChar, TArg>(ITokenizerResumptionPoint<TChar, TArg> resumptionPoint, TArg arg) : Tokenizer<TChar>
    {
        public override bool TryGetNextToken(ref ParserInputReader<TChar> input,
            ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result) =>
            resumptionPoint.TryGetNextToken(ref input, semanticProvider, arg, out result);
    }
}
