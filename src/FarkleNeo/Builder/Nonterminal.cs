// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Builder;

/// <summary>
/// Provides factory methods to define nonterminals.
/// </summary>
public static class Nonterminal
{
    /// <summary>
    /// Creates a nonterminal that produces a value, whose productions must be assigned at a later time.
    /// </summary>
    /// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
    /// <param name="name">The nonterminal's name.</param>
    /// <seealso cref="Nonterminal{T}.SetProductions(IProduction{T}[])"/>
    /// <seealso cref="Nonterminal{T}.SetProductions(ReadOnlySpan{IProduction{T}})"/>
    public static Nonterminal<T> Create<T>(string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        return new(name);
    }

    /// <summary>
    /// Creates a nonterminal that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol<T> Create<T>(string name, params IProduction<T>[] productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(productions);
        return Create<T>(name, productions.AsSpan());
    }

    /// <summary>
    /// Creates a nonterminal that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol<T> Create<T>(string name, params ReadOnlySpan<IProduction<T>> productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        var builder = ImmutableArray.CreateBuilder<IProduction>(productions.Length);
        foreach (var production in productions)
        {
            builder.Add(production.Production);
        }
        return new Nonterminal<T>(name, builder.MoveToImmutable());
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value and whose productions
    /// must be assigned at a later time.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ProductionBuilder[])"/>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ReadOnlySpan{ProductionBuilder})"/>
    public static Untyped.Nonterminal CreateUntyped(string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        return new(name);
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions, represented as <see cref="ProductionBuilder"/>
    /// objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol CreateUntyped(string name, params ProductionBuilder[] productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(productions);
        return CreateUntyped(name, productions.AsSpan());
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions, represented as <see cref="ProductionBuilder"/>
    /// objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol CreateUntyped(string name, params ReadOnlySpan<ProductionBuilder> productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        return new Untyped.Nonterminal(name, ImmutableArray<IProduction>.CastUp(productions.ToImmutableArray()));
    }
}

/// <summary>
/// Represents a nonterminal symbol in a grammar to be built that produces a value,
/// and allows setting its productions after its creation.
/// </summary>
/// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
/// <remarks>
/// In Farkle, builder objects are usually immutable. This exception exists to support
/// defining recursive nonterminals.
/// </remarks>
public sealed class Nonterminal<T> : INonterminal, IGrammarSymbol<T>
{
    /// <summary>
    /// Inner untyped nonterminal that contains the logic of setting the productions.
    /// As with most other places in the builder, the types are erased at the first
    /// opportunity. This allows us to prevent duplicating the logic in both the typed
    /// and untyped nonterminals.
    /// </summary>
    private readonly Untyped.Nonterminal _innerNonterminal;

    /// <inheritdoc/>
    public string Name => _innerNonterminal.Name;

    ISymbolBase IGrammarBuilder.Symbol => this;

    internal Nonterminal(string name, ImmutableArray<IProduction> productions = default) =>
        _innerNonterminal = new(name, productions);

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">An array with the productions to set.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(params IProduction<T>[] productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(productions);
        SetProductions(productions.AsSpan());
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">The productions to set.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(params ReadOnlySpan<IProduction<T>> productions)
    {
        var builder = ImmutableArray.CreateBuilder<IProduction>(productions.Length);
        foreach (var production in productions)
        {
            builder.Add(production.Production);
        }
        _innerNonterminal.SetProductions(builder.MoveToImmutable());
    }

    ImmutableArray<IProduction> INonterminal.FreezeAndGetProductions() =>
        _innerNonterminal.FreezeAndGetProductions();
}
