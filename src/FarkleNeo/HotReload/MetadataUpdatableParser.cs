// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.HotReload;

/// <summary>
/// Provides a <see cref="CharParser{T}"/> that wraps a <see cref="CharParser{T}"/>
/// created from a factory function, and refreshes it when Hot Reload is performed.
/// </summary>
/// <typeparam name="T">The parser's result type.</typeparam>
/// <typeparam name="TOriginal">The original parser's result type, before changing its semantic provider.</typeparam>
internal sealed class MetadataUpdatableParser<T, TOriginal> : CharParser<T>, IMetadataUpdatable, IParserStateContextFactory<char, T>
{
    /// <summary>
    /// The <see cref="Type"/> to key Hot Reload event notifications on.
    /// </summary>
    private readonly Type _metadataUpdateKey;

    /// <summary>
    /// The factory for the original parser.
    /// </summary>
    /// <remarks>
    /// It is assumed that calling this function will build a parser and take a lot of time,
    /// so it is only called when needed. If the original parser has not been invalidated,
    /// customizing the parser will not cause it to be rebuilt.
    /// </remarks>
    private readonly Func<CharParser<TOriginal>> _parserFactory;

    /// <summary>
    /// A function that potentially changes the tokenizer of the original parser.
    /// </summary>
    /// <remarks>
    /// It is assumed that calling this function is fast. The initial value of this field
    /// is the identity function.
    /// </remarks>
    private readonly Func<CharParser<TOriginal>, CharParser<TOriginal>> _tokenizerEnricher;

    /// <summary>
    /// A function that potentially changes the semantic provider of the original parser,
    /// and its return type.
    /// </summary>
    /// <remarks>
    /// It is assumed that calling this function is fast. The initial value of this field
    /// is the identity function.
    /// </remarks>
    private readonly Func<CharParser<TOriginal>, CharParser<T>> _semanticProviderEnricher;

    /// <summary>
    /// A tuple that holds the original and the transformed parser.
    /// </summary>
    /// <remarks>
    /// This is a reference tuple to support atomically clearing both when
    /// Hot Reload is performed.
    /// </remarks>
    private volatile Tuple<CharParser<TOriginal>, CharParser<T>>? _parser;

    private CharParser<T> TransformParser(CharParser<TOriginal> parser) =>
        _semanticProviderEnricher(_tokenizerEnricher(parser));

    private CharParser<T> GetParser()
    {
        return _parser?.Item2 ?? CreateParser();

        CharParser<T> CreateParser()
        {
            var parser = _parserFactory();
            var transformedParser = TransformParser(parser);
            Interlocked.CompareExchange(ref _parser, Tuple.Create(parser, transformedParser), null);
            return _parser.Item2;
        }
    }

    private CharParser<T> WithTokenizerEnricher(Func<CharParser<TOriginal>, CharParser<TOriginal>> fTokenizer) =>
        new MetadataUpdatableParser<T, TOriginal>(_metadataUpdateKey, _parserFactory, fTokenizer, _semanticProviderEnricher, _parser?.Item1);

    private CharParser<TNew> WithSemanticProviderEnricher<TNew>(Func<CharParser<TOriginal>, CharParser<TNew>> fSemanticProvider) =>
        new MetadataUpdatableParser<TNew, TOriginal>(_metadataUpdateKey, _parserFactory, _tokenizerEnricher, fSemanticProvider, _parser?.Item1);

    public MetadataUpdatableParser(Type metadataUpdateKey, Func<CharParser<TOriginal>> parserFactory, Func<CharParser<TOriginal>, CharParser<TOriginal>> tokenizerEnricher,
        Func<CharParser<TOriginal>, CharParser<T>> semanticProviderEnricher, CharParser<TOriginal>? originalParser)
    {
        _metadataUpdateKey = metadataUpdateKey;
        _parserFactory = parserFactory;
        _semanticProviderEnricher = semanticProviderEnricher;
        _tokenizerEnricher = tokenizerEnricher;
        if (originalParser is not null)
        {
            _parser = Tuple.Create(originalParser, TransformParser(originalParser));
        }
#if NET6_0_OR_GREATER
        MetadataUpdatableManager.Register(metadataUpdateKey, this);
#endif
    }

    public override void Run(ref ParserInputReader<char> input, ref ParserCompletionState<T> completionState) =>
        GetParser().Run(ref input, ref completionState);

    internal override IGrammarProvider GetGrammarProvider() => GetParser().GetGrammarProvider();

    internal override Tokenizer<char> GetTokenizer() => GetParser().GetTokenizer();

    internal override object? GetServiceCore(Type serviceType)
    {
        if (serviceType == typeof(IParserStateContextFactory<char, T>))
        {
            return this;
        }

        return GetParser().GetServiceCore(serviceType);
    }

    private protected override CharParser<TNew> WithSemanticProviderCore<TNew>(ISemanticProvider<char, TNew> semanticProvider) =>
        WithSemanticProviderEnricher(p => p.WithSemanticProvider(semanticProvider));

    private protected override CharParser<TNew> WithSemanticProviderCore<TNew>(Func<IGrammarProvider, ISemanticProvider<char, TNew>> semanticProviderFactory) =>
        WithSemanticProviderEnricher(p => p.WithSemanticProvider(semanticProviderFactory));

    private protected override CharParser<T> WithTokenizerCore(Tokenizer<char> tokenizer) =>
        WithTokenizerEnricher(p => p.WithTokenizer(tokenizer));

    private protected override CharParser<T> WithTokenizerChainCore(ReadOnlySpan<ChainedTokenizerComponent<char>> components)
    {
        // Copy span to array to be able to capture it in the closure.
        var componentsArray = components.ToArray();
        return WithTokenizerEnricher(p => p.WithTokenizerChain(componentsArray.AsSpan()));
    }

    public void ClearCache()
    {
        _parser = null;
    }

    ParserStateContext<char, T> IParserStateContextFactory<char, T>.CreateContext(ParserStateContextOptions? options) =>
        ParserStateContext.Create(GetParser(), options);
}

internal static class MetadataUpdatableParser
{
    public static MetadataUpdatableParser<T, T> Create<T>(Type metadataUpdateKey, Func<CharParser<T>> parserFactory) =>
        new(metadataUpdateKey, parserFactory, p => p, p => p, parserFactory());
}
