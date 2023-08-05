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
    internal static ChainedTokenizerState<TChar>? GetChainedTokenizerStateOrNull<TChar>(this in ParserState parserState)
    {
        if (parserState.TryGetValue(typeof(ChainedTokenizerState<TChar>), out object? value))
        {
            Debug.Assert(value is ChainedTokenizerState<TChar>);
            return Unsafe.As<ChainedTokenizerState<TChar>>(value);
        }
        return null;
    }

    internal static ChainedTokenizerState<TChar> GetOrCreateChainedTokenizerState<TChar>(this ref ParserState parserState)
    {
        if (!parserState.TryGetValue(typeof(ChainedTokenizerState<TChar>), out object? value))
        {
            value = new ChainedTokenizerState<TChar>();
            parserState.SetValue(typeof(ChainedTokenizerState<TChar>), value);
        }
        Debug.Assert(value is ChainedTokenizerState<TChar>);
        return Unsafe.As<ChainedTokenizerState<TChar>>(value);
    }

    private static void SuspendTokenizerCore<TChar>(this ref ParserState state, Tokenizer<TChar> tokenizer)
    {
        var tokenizerState = state.GetOrCreateChainedTokenizerState<TChar>();
        if (tokenizerState.TokenizerToResume is not null)
        {
            ThrowHelpers.ThrowInvalidOperationException(Resources.Tokenizer_AlreadySuspended);
        }
        tokenizerState.TokenizerToResume = tokenizer;
    }

    /// <summary>
    /// Suspends the tokenization process and sets it to resume at the specified <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer processes.</typeparam>
    /// <param name="state">The state of the parsing operation.</param>
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
    public static void SuspendTokenizer<TChar>(this ref ParserState state, Tokenizer<TChar> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer);
        state.SuspendTokenizerCore(tokenizer);
    }

    /// <summary>
    /// Suspends the tokenization process and sets it to resume at the specified
    /// <see cref="ITokenizerResumptionPoint{TChar, TArg}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters the tokenizer processes.</typeparam>
    /// <typeparam name="TArg">The type of <paramref name="arg"/>.</typeparam>
    /// <param name="state">The state of the parsing operation.</param>
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
    public static void SuspendTokenizer<TChar, TArg>(this ref ParserState state,
        ITokenizerResumptionPoint<TChar, TArg> resumptionPoint, TArg arg)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(resumptionPoint);
        state.SuspendTokenizerCore(new TokenizerResumptionPoint<TChar, TArg>(resumptionPoint, arg));
    }

    private sealed class TokenizerResumptionPoint<TChar, TArg> : Tokenizer<TChar>
    {
        private readonly ITokenizerResumptionPoint<TChar, TArg> _resumptionPoint;
        private readonly TArg _arg;

        public TokenizerResumptionPoint(ITokenizerResumptionPoint<TChar, TArg> resumptionPoint, in TArg arg)
        {
            _resumptionPoint = resumptionPoint;
            _arg = arg;
        }

        public override bool TryGetNextToken(ref ParserInputReader<TChar> reader,
            ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result) =>
            _resumptionPoint.TryGetNextToken(ref reader, semanticProvider, _arg, out result);
    }
}
