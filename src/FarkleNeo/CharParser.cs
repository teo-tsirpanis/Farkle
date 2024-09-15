// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Implementation;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle;

/// <summary>
/// Provides the base class of Farkle's default parsers.
/// </summary>
/// <typeparam name="T">The type of objects the parser produces in case of success.</typeparam>
/// <remarks>
/// <para>
/// This class is the replacement of the <c>RuntimeFarkle</c> class of Farkle 6.
/// It extends <see cref="IParser{TChar, T}"/> with features like swapping the parser's
/// <see cref="Tokenizer{TChar}"/> and <see cref="ISemanticProvider{TChar, T}"/>,
/// getting the parser's <see cref="Grammar"/> and representing parsers that will
/// always fail because of problems with the grammar.
/// </para>
/// <para>
/// <see cref="CharParser{T}"/>s are immutable, stateless and thread-safe.
/// Methods that customize them return new instances.
/// </para>
/// <para>
/// Unlike <see cref="IParser{TChar, T}"/>, <see cref="CharParser{T}"/>
/// cannot be inherited by user code.
/// </para>
/// </remarks>
public abstract class CharParser<T> : IParser<char, T>
{
    private protected CharParser() { }

    private protected abstract IGrammarProvider GetGrammarProvider();

    private protected abstract Tokenizer<char> GetTokenizer();

    private protected virtual object? GetServiceCore(Type serviceType)
    {
        if (serviceType == typeof(IGrammarProvider))
        {
            return GetGrammarProvider();
        }

        return null;
    }

    private protected abstract CharParser<T> WithTokenizerCore(Tokenizer<char> tokenizer);

    private protected virtual CharParser<T> WithTokenizerChainCore(ReadOnlySpan<ChainedTokenizerComponent<char>> components) =>
        WithTokenizerCore(Tokenizer.CreateChain(components, GetGrammarProvider(), GetTokenizer()));

    private protected abstract CharParser<TNew> WithSemanticProviderCore<TNew>(ISemanticProvider<char, TNew> semanticProvider);

    private protected virtual CharParser<TNew> WithSemanticProviderCore<TNew>(Func<IGrammarProvider, ISemanticProvider<char, TNew>> semanticProviderFactory) =>
        WithSemanticProviderCore(semanticProviderFactory(GetGrammarProvider()));

    /// <summary>
    /// Implements <see cref="IServiceProvider.GetService(Type)"/>.
    /// </summary>
    /// <remarks>
    /// The following services are always provided by <see cref="CharParser{T}"/>:
    /// <list type="bullet">
    /// <item><see cref="IGrammarProvider"/></item>
    /// </list>
    /// </remarks>
    public object? GetService(Type serviceType)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(serviceType);
        return GetServiceCore(serviceType);
    }

    /// <inheritdoc/>
    public abstract void Run(ref ParserInputReader<char> input, ref ParserCompletionState<T> completionState);

    /// <summary>
    /// Whether the <see cref="CharParser{T}"/> will always fail because of problems
    /// with the grammar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error can be retrieved by parsing an empty string.
    /// </para>
    /// <para>
    /// If the problem is in the grammar's lexical analysis tables,
    /// the parser can be fixed by changing the tokenizer.
    /// </para>
    /// </remarks>
    public bool IsFailing { get; private protected init; }

    /// <summary>
    /// Gets the <see cref="Grammar"/> used by the <see cref="CharParser{T}"/>.
    /// </summary>
    public Grammar GetGrammar() => GetGrammarProvider().GetGrammar();

    /// <summary>
    /// Changes the semantic provider of the <see cref="CharParser{T}"/>
    /// to an <see cref="ISemanticProvider{TChar, T}"/>.
    /// </summary>
    /// <typeparam name="TNew">The result type of the new parser.</typeparam>
    /// <param name="semanticProvider">The semantic provider of the new parser.</param>
    /// <returns>A <see cref="CharParser{T}"/> that returns <typeparamref name="TNew"/> on success
    /// and has <paramref name="semanticProvider"/> as its semantic provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="semanticProvider"/>
    /// is <see langword="null"/>.</exception>
    public CharParser<TNew> WithSemanticProvider<TNew>(ISemanticProvider<char, TNew> semanticProvider)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(semanticProvider);
        return WithSemanticProviderCore(semanticProvider);
    }

    /// <summary>
    /// Changes the semantic provider of the <see cref="CharParser{T}"/>
    /// to an <see cref="ISemanticProvider{TChar, T}"/> that depends on
    /// the parser's grammar.
    /// </summary>
    /// <typeparam name="TNew">The result type of the new parser.</typeparam>
    /// <param name="semanticProviderFactory">A delegate that accepts an <see cref="IGrammarProvider"/>
    /// and returns a semantic provider.</param>
    /// <returns>A <see cref="CharParser{T}"/> that returns <typeparamref name="TNew"/> on success
    /// and has the result of <paramref name="semanticProviderFactory"/> as its semantic provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="semanticProviderFactory"/>
    /// is <see langword="null"/>.</exception>
    /// <remarks>
    /// In certain failing parsers, <paramref name="semanticProviderFactory"/> will not be called.
    /// </remarks>
    public CharParser<TNew> WithSemanticProvider<TNew>(Func<IGrammarProvider, ISemanticProvider<char, TNew>> semanticProviderFactory)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(semanticProviderFactory);
        return WithSemanticProviderCore(semanticProviderFactory);
    }

    /// <summary>
    /// Changes the tokenizer of the <see cref="CharParser{T}"/>
    /// to a <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <param name="tokenizer">The tokenizer of the new parser.</param>
    /// <returns>A <see cref="CharParser{T}"/> with <paramref name="tokenizer"/>
    /// as its tokenizer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizer"/>
    /// is <see langword="null"/>.</exception>
    /// <remarks>
    /// In certain failing parsers, this method will have no effect and return
    /// <see langword="this"/>.
    /// </remarks>
    public CharParser<T> WithTokenizer(Tokenizer<char> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer);
        return WithTokenizerCore(tokenizer);
    }

    /// <summary>
    /// Changes the tokenizer of the <see cref="CharParser{T}"/> to a
    /// <see cref="Tokenizer{TChar}"/> that depends on a grammar.
    /// </summary>
    /// <param name="tokenizerFactory">A delegate that accepts an <see cref="IGrammarProvider"/>
    /// and returns a tokenizer.</param>
    /// <returns>A <see cref="CharParser{T}"/> with the result of <paramref name="tokenizerFactory"/>
    /// as its tokenizer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizerFactory"/>
    /// is <see langword="null"/>.</exception>
    /// <remarks>
    /// In certain failing parsers this method will have no effect and return <see langword="this"/>.
    /// </remarks>
    public CharParser<T> WithTokenizer(Func<IGrammarProvider, Tokenizer<char>> tokenizerFactory)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizerFactory);
        return WithTokenizerChain([ChainedTokenizerComponent<char>.Create(tokenizerFactory)]);
    }

    /// <summary>
    /// Changes the tokenizer of the <see cref="CharParser{T}"/> to a chained tokenizer
    /// to be built from a sequence of <see cref="ChainedTokenizerComponent{TChar}"/>s.
    /// </summary>
    /// <param name="components">The sequence of chained tokenizer components.</param>
    /// <exception cref="ArgumentException"><paramref name="components"/> is empty.</exception>
    /// <remarks>
    /// In certain failing parsers this method will have no effect and return <see langword="this"/>.
    /// </remarks>
    public CharParser<T> WithTokenizerChain(ReadOnlySpan<ChainedTokenizerComponent<char>> components)
    {
        return WithTokenizerChainCore(components);
    }

    /// <inheritdoc cref="WithTokenizerChain(ReadOnlySpan{ChainedTokenizerComponent{char}})"/>
    public CharParser<T> WithTokenizerChain(params ChainedTokenizerComponent<char>[] components)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(components);
        return WithTokenizerChain(components.AsSpan());
    }
}

/// <summary>
/// Provides factory methods to create <see cref="CharParser{T}"/>s.
/// </summary>
public static class CharParser
{
    /// <summary>
    /// Creates a <see cref="CharParser{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
    /// <param name="grammar">The <see cref="Grammar"/> the parser will use.</param>
    /// <param name="semanticProvider">The <see cref="ISemanticProvider{TChar, T}"/> the parser will use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grammar"/> or <paramref name="semanticProvider"/>
    /// is <see langword="null"/>.</exception>
    public static CharParser<T> Create<T>(Grammar grammar, ISemanticProvider<char, T> semanticProvider)
    {
        Tokenizer<char> tokenizer = Tokenizer.Create<char>(grammar, throwIfError: false);
        return Create(grammar, tokenizer, semanticProvider, null);
    }

    /// <inheritdoc cref="Create{T}(Grammar, ISemanticProvider{char, T})"/>
    /// <param name="tokenizer">The <see cref="Tokenizer{TChar}"/> the parser will use.</param>
    /// <param name="customError">A custom error object to be used instead of the default errors. This
    /// is typically provided by the builder, which has a more complete picture of what went wrong.</param>
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
    // See https://github.com/dotnet/roslyn/issues/40325
    internal static CharParser<T> Create<T>(Grammar grammar, Tokenizer<char> tokenizer, ISemanticProvider<char, T> semanticProvider, object? customError)
#pragma warning restore CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(grammar);
        ArgumentNullExceptionCompat.ThrowIfNull(semanticProvider);

        if (grammar.IsUnparsable(out string? errorKey))
        {
            return Fail(errorKey);
        }
        if (grammar.LrStateMachine is not { } lrStateMachine)
        {
            return Fail(nameof(Resources.Parser_GrammarLrMissing));
        }
        if (lrStateMachine.HasConflicts)
        {
            return Fail(nameof(Resources.Parser_GrammarLrProblem));
        }

        return new DefaultParser<T>(grammar, lrStateMachine, semanticProvider, tokenizer);

        CharParser<T> Fail(string resourceKey) =>
            new FailingCharParser<T>(customError ?? LocalizedDiagnostic.Create(resourceKey), grammar);
    }

    /// <summary>
    /// Creates a <see cref="CharParser{T}"/> that does not perform any semantic analysis.
    /// </summary>
    /// <typeparam name="T">The type of objects the syntax checker will return in case of success.
    /// Must be a reference type and usually it is <see cref="object"/>
    /// or <see cref="T:Microsoft.FSharp.Core.Unit"/>.</typeparam>
    /// <param name="grammar">The <see cref="Grammar"/> the syntax checker will use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grammar"/> is <see langword="null"/>.</exception>
    /// <remarks>Syntax checkers always return <see langword="null"/> in case of success.</remarks>
    public static CharParser<T?> CreateSyntaxChecker<T>(Grammar grammar) where T : class =>
        Create(grammar, SyntaxChecker<char, T>.Instance);

    /// <summary>
    /// Creates a <see cref="CharParser{T}"/> that does not perform any semantic analysis.
    /// </summary>
    /// <param name="grammar">The <see cref="Grammar"/> the syntax checker will use.</param>
    /// <remarks>Syntax checkers always return <see langword="null"/> in case of success.</remarks>
    public static CharParser<object?> CreateSyntaxChecker(Grammar grammar) =>
        CreateSyntaxChecker<object>(grammar);

    /// <summary>
    /// Converts a <see cref="CharParser{T}"/> to a syntax checker with a user-defined return type.
    /// </summary>
    /// <seealso cref="CreateSyntaxChecker{T}(Grammar)"/>
    public static CharParser<TNew?> ToSyntaxChecker<T, TNew>(this CharParser<T> parser) where TNew : class =>
        parser.WithSemanticProvider(SyntaxChecker<char, TNew>.Instance);

    /// <summary>
    /// Converts a <see cref="CharParser{T}"/> to a syntax checker.
    /// </summary>
    /// <seealso cref="CreateSyntaxChecker(Grammar)"/>
    public static CharParser<object?> ToSyntaxChecker<T>(this CharParser<T> parser) =>
        parser.ToSyntaxChecker<T, object>();
}
