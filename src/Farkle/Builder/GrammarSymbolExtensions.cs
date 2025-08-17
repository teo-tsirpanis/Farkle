// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Contains extension methods that to set configuration options on <see cref="IGrammarSymbol"/>
/// and <see cref="IGrammarSymbol{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These methods apply to specific symbols within the grammar and do not modify the object they
/// are called on. Instead, they return a new object with the requested configuration option changed.
/// </para>
/// <para>
/// Changing configuration on a symbol does not change the symbol's identity and the changed symbol
/// instance will be treated as the same symbol as before when building the grammar. Consult the
/// documentation of each method for information on what happens when a symbol exists in a grammar
/// with varying configuration options.
/// </para>
/// </remarks>
public static class GrammarSymbolExtensions
{
    internal static ref readonly GrammarSymbolOptions GetOptions(this IGrammarSymbol builder) =>
        ref builder is GrammarSymbolWrapper wrapper ? ref wrapper.Options : ref GrammarSymbolOptions.Default;

    private static IGrammarSymbol WithOptions(this IGrammarSymbol symbol, in GrammarSymbolOptions options)
    {
        return symbol is GrammarSymbolWrapper wrapper
            ? wrapper.WithOptions(in options)
            : new GrammarSymbolWrapper(in options, symbol.Symbol);
    }

    private static IGrammarSymbol<T> WithOptions<T>(this IGrammarSymbol<T> symbol, in GrammarSymbolOptions options)
    {
        return symbol is GrammarSymbolWrapper<T> wrapper
            ? wrapper.WithOptions(in options)
            : new GrammarSymbolWrapper<T>(in options, symbol.Symbol);
    }

    /// <summary>
    /// Changes the type of <see cref="IGrammarSymbol"/> to a generic <see cref="IGrammarSymbol{T}"/>
    /// of type <see cref="object"/>, forcing it to return a value.
    /// </summary>
    /// <param name="symbol">The grammar symbol.</param>
    /// <returns>An <see cref="IGrammarSymbol{T}"/> that returns the object <paramref name="symbol"/>
    /// would return. If <paramref name="symbol"/> had been created with the untyped API, the returned
    /// object will be <see langword="null"/>.</returns>
    public static IGrammarSymbol<object?> Cast(this IGrammarSymbol symbol)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        if (symbol is IGrammarSymbol<object?> b)
        {
            return b;
        }
        return new GrammarSymbolWrapper<object?>(GrammarSymbolOptions.Default, symbol.Symbol);
    }

    /// <summary>
    /// Adds a special name to the symbol, which is used to uniquely identify it within the grammar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a grammar has multiple symbols with the same special name, building will fail.
    /// </para>
    /// <para>
    /// Calling this method multiple times will add multiple special names to the symbol.
    /// </para>
    /// </remarks>
    /// <seealso cref="IGrammarProvider.GetSymbolFromSpecialName"/>
    public static IGrammarSymbol AddSpecialName(this IGrammarSymbol symbol, string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return symbol.WithOptions(symbol.GetOptions().AddSpecialName(name));
    }

    /// <inheritdoc cref="AddSpecialName(IGrammarSymbol, string)"/>
    public static IGrammarSymbol<T> AddSpecialName<T>(this IGrammarSymbol<T> symbol, string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return symbol.WithOptions(symbol.GetOptions().AddSpecialName(name));
    }

    /// <summary>
    /// Creates a symbol that can match the given symbol multiple times.
    /// </summary>
    /// <typeparam name="T">The type of values the symbol returns.</typeparam>
    /// <typeparam name="TCollection">The type of collection to place the values of
    /// the symbol in. Must implement <see cref="ICollection{T}"/>.</typeparam>
    /// <param name="symbol">The symbol to match multiple times.</param>
    /// <param name="atLeastOnce">Whether <paramref name="symbol"/> must be matched
    /// at least once. Optional, defaults to <see langword="false"/></param>
    public static IGrammarSymbol<TCollection> Many<T, TCollection>(
        this IGrammarSymbol<T> symbol, bool atLeastOnce = false)
        where TCollection : ICollection<T>, new()
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        Nonterminal<TCollection> nont = Nonterminal.Create<TCollection>($"{symbol.Name}{(atLeastOnce ? " Non-empty" : "")} {typeof(TCollection).Name}");
        if (atLeastOnce)
        {
            nont.SetProductions(
                symbol.Finish(x => new TCollection { x }),
                nont.Extended().Extend(symbol).Finish((c, x) => { c.Add(x); return c; })
            );
        }
        else
        {
            nont.SetProductions(
                ProductionBuilder.Empty.Finish(() => new TCollection()),
                nont.Extended().Extend(symbol).Finish((c, x) => { c.Add(x); return c; })
            );
        }
        return nont;
    }

    /// <summary>
    /// Creates a symbol that can match the given symbol multiple times, separated
    /// by another symbol.
    /// </summary>
    /// <typeparam name="T">The type of values the symbol returns.</typeparam>
    /// <typeparam name="TCollection">The type of collection to place the values of
    /// the symbol in. Must implement <see cref="ICollection{T}"/>.</typeparam>
    /// <param name="symbol">The symbol to match multiple times.</param>
    /// <param name="separator">The symbol to match between the instances of
    /// <paramref name="symbol"/>.</param>
    /// <param name="atLeastOnce">Whether <paramref name="symbol"/> must be matched
    /// at least once. Optional, defaults to <see langword="false"/></param>
    public static IGrammarSymbol<TCollection> SeparatedBy<T, TCollection>(
        this IGrammarSymbol<T> symbol, IGrammarSymbol separator, bool atLeastOnce = false)
        where TCollection : ICollection<T>, new()
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        Nonterminal<TCollection> nont = Nonterminal.Create<TCollection>($"{symbol.Name}{(atLeastOnce ? " Non-empty" : "")} {typeof(TCollection).Name} Separated By {separator.Name}");
        if (atLeastOnce)
        {
            nont.SetProductions(
                symbol.Finish(x => new TCollection { x }),
                nont.Extended().Append(separator).Extend(symbol).Finish((c, x) => { c.Add(x); return c; })
            );
        }
        else
        {
            nont.SetProductions(
                ProductionBuilder.Empty.Finish(() => new TCollection()),
                SeparatedBy<T, TCollection>(symbol, separator, true).AsProduction()
            );
        }
        return nont;
    }

    /// <summary>
    /// Creates a symbol that can match either the given symbol once, or nothing.
    /// In the latter case it returns <see langword="default"/>.
    /// </summary>
    /// <typeparam name="T">The type of values the symbol returns.</typeparam>
    /// <param name="symbol">The symbol to make nullable.</param>
    /// <seealso cref="Optional"/>
    public static IGrammarSymbol<T?> Nullable<T>(this IGrammarSymbol<T> symbol)
        where T : struct
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return Nonterminal.Create($"{symbol.Name} Maybe",
            symbol.Finish(x => (T?)x),
            ProductionBuilder.Empty.FinishConstant<T?>(null)
        );
    }

    /// <summary>
    /// Creates a symbol that can match either the given symbol once, or nothing.
    /// In the latter case it returns <see langword="null"/>.
    /// </summary>
    /// <seealso cref="Nullable"/>
    public static IGrammarSymbol<T?> Optional<T>(this IGrammarSymbol<T> symbol)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return Nonterminal.Create($"{symbol.Name} Maybe",
            (IProduction<T?>)symbol.AsProduction(),
            ProductionBuilder.Empty.FinishConstant<T?>(default)
        );
    }

    /// <summary>
    /// Creates a symbol that matches the given symbol and then applies a
    /// transformation to its returning value.
    /// </summary>
    /// <typeparam name="T">The type of values the symbol returns.</typeparam>
    /// <typeparam name="TNew">The type of values the new symbol will return.</typeparam>
    /// <param name="symbol">The symbol to transform.</param>
    /// <param name="selector">The transformation to apply to the value of <paramref name="symbol"/>.</param>
    public static IGrammarSymbol<TNew> Select<T, TNew>(this IGrammarSymbol<T> symbol, Func<T, TNew> selector)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        ArgumentNullExceptionCompat.ThrowIfNull(selector);
        return Nonterminal.Create($"{symbol.Name} :?> {typeof(TNew).Name}", symbol.Finish(selector));
    }
}
