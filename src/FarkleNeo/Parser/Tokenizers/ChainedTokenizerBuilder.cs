// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Farkle.Parser.Tokenizers;

/// <summary>
/// Provides an API to compose multiple <see cref="Tokenizer{TChar}"/>s into a chain.
/// </summary>
/// <typeparam name="TChar">The type of characters the tokenizers process.</typeparam>
/// <remarks>
/// <para>
/// A chained tokenizer invokes the tokenizers in order, until one of them returns with
/// a result. Tokenizers can set to contunue after returning without resetting the chain
/// by calling <see cref="TokenizerExtensions.SuspendTokenizer{TChar}(ref ParserState, Tokenizer{TChar})"/>
/// or an overload thereof.
/// </para>
/// <para>
/// Chaining and suspending tokenizers is designed with the goal of allowing tokenizers
/// written by different people to interact. For this reason, manually calling
/// <see cref="Tokenizer{TChar}.TryGetNextToken"/> within a tokenizer can break this
/// mechanism and should not be done.
/// </para>
/// <pare>
/// This is an immutable class storing a list of tokenizers. Methods that add tokenizers
/// return a new instance with the given tokenizer at the end.
/// </pare>
/// </remarks>
/// <seealso cref="CharParser{T}.WithTokenizer(ChainedTokenizerBuilder{char})"/>
public sealed class ChainedTokenizerBuilder<TChar>
{
    private readonly ImmutableList<object?> _items;

    private static readonly ChainedTokenizerBuilder<TChar> s_default = new(ImmutableList.Create((object?)null));

    private ChainedTokenizerBuilder(ImmutableList<object?> items)
    {
        _items = items;
    }

    private ChainedTokenizerBuilder<TChar> AddImpl(object? item)
    {
        Debug.Assert(item is (Tokenizer<TChar> and not ChainedTokenizer<TChar>) or Func<IGrammarProvider, Tokenizer<TChar>> or null);
        return new(_items.Add(item));
    }

    private ChainedTokenizerBuilder<TChar> AddMany<TCollection>(TCollection items)
        where TCollection : IEnumerable<object?>
    {
        ChainedTokenizerBuilder<TChar> result = this;
        foreach (object? item in items)
        {
            result = result.AddImpl(item);
        }
        return result;
    }

    /// <summary>
    /// Creates a <see cref="ChainedTokenizerBuilder{TChar}"/> that starts with the default tokenizer.
    /// </summary>
    /// <remarks>
    /// When passing the builder to <see cref="CharParser{T}.WithTokenizer(ChainedTokenizerBuilder{char})"/>,
    /// the default tokenizer is the parser's existing tokenizer. Otherwise it is
    /// the tokenizer specified in <see cref="Build"/>, if provided.
    /// </remarks>
    public static ChainedTokenizerBuilder<TChar> CreateDefault() => s_default;

    /// <summary>
    /// Adds a <see cref="Tokenizer{TChar}"/> to the end of the chain.
    /// </summary>
    /// <param name="tokenizer">The tokenizer to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizer"/> is null.</exception>
    /// <seealso cref="Create(Tokenizer{TChar})"/>
    /// <seealso cref="CharParser{T}.WithTokenizer(Tokenizer{char})"/>
    public ChainedTokenizerBuilder<TChar> Add(Tokenizer<TChar> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer);
        if (tokenizer is ChainedTokenizer<TChar> chained)
        {
            return AddMany(chained.Components);
        }
        return AddImpl(tokenizer);
    }

    /// <summary>
    /// Adds a <see cref="Tokenizer{TChar}"/> that depends on a grammar to the end of the chain.
    /// </summary>
    /// <param name="tokenizerFactory">A delegate that accepts an <see cref="IGrammarProvider"/>
    /// and returns a tokenizer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizerFactory"/> is null.</exception>
    /// <seealso cref="Create(Tokenizer{TChar})"/>
    /// <seealso cref="CharParser{T}.WithTokenizer(Tokenizer{char})"/>
    public ChainedTokenizerBuilder<TChar> Add(Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizerFactory);
        return AddImpl(tokenizerFactory);
    }

    /// <summary>
    /// Adds the tokenizers of another <see cref="ChainedTokenizerBuilder{TChar}"/> to the end of the chain.
    /// </summary>
    /// <param name="builder">The chained tokenizer builder to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public ChainedTokenizerBuilder<TChar> Add(ChainedTokenizerBuilder<TChar> builder)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(builder);
        return AddMany(builder._items);
    }

    /// <summary>
    /// Adds the default tokenizer to the end of the chain.
    /// </summary>
    /// <seealso cref="CreateDefault"/>
    public ChainedTokenizerBuilder<TChar> AddDefault() => AddImpl(null);

    /// <summary>
    /// Creates a <see cref="Tokenizer{TChar}"/> from the tokenizers added to the chain.
    /// </summary>
    /// <param name="grammar">The <see cref="IGrammarProvider"/> to pass to the delegates
    /// given in <see cref="Create(Func{IGrammarProvider, Tokenizer{TChar}})"/>
    /// and <see cref="Add(Func{IGrammarProvider, Tokenizer{TChar}})"/>.
    /// This parameter is optional if no such delegates have been added.</param>
    /// <param name="defaultTokenizer">The tokenizer to use in place of <see cref="CreateDefault"/> or <see cref="AddDefault"/>.</param>
    public Tokenizer<TChar> Build(IGrammarProvider? grammar = null, Tokenizer<TChar>? defaultTokenizer = null)
    {
        var builder = ImmutableArray.CreateBuilder<Tokenizer<TChar>>(_items.Count);
        for (int i = 0; i < _items.Count; i++)
        {
            object? item = _items[i];
            switch (item)
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
                default:
                    Debug.Assert(item is null);
                    if (defaultTokenizer is null)
                    {
                        ThrowHelpers.ThrowInvalidOperationException(Resources.ChainedTokenizerBuilder_NoDefaultTokenizer);
                    }
                    builder.Add(defaultTokenizer);
                    break;
            }
        }
        var components = builder.Count == builder.Capacity ? builder.MoveToImmutable() : builder.ToImmutable();
        return ChainedTokenizer<TChar>.Create(components);
    }

    /// <summary>
    /// Creates a <see cref="ChainedTokenizerBuilder{TChar}"/> that starts with a <see cref="Tokenizer{TChar}"/>.
    /// </summary>
    /// <param name="tokenizer">The tokenizer to start the chain with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizer"/> is null.</exception>
    public static ChainedTokenizerBuilder<TChar> Create(Tokenizer<TChar> tokenizer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizer);
        if (tokenizer is ChainedTokenizer<TChar> chained)
        {
            return new(ImmutableList.CreateRange(chained.Components.CastArray<object?>()));
        }
        return new(ImmutableList.Create((object?)tokenizer));
    }

    /// <summary>
    /// Creates a <see cref="ChainedTokenizerBuilder{TChar}"/> that starts with a <see cref="Tokenizer{TChar}"/>
    /// that depends on a grammar.
    /// </summary>
    /// <param name="tokenizerFactory">The tokenizer to start the chain with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizerFactory"/> is null.</exception>
    public static ChainedTokenizerBuilder<TChar> Create(Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(tokenizerFactory);
        return new(ImmutableList.Create((object?)tokenizerFactory));
    }
}
