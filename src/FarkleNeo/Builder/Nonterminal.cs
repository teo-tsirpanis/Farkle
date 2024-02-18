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
    /// <seealso cref="Nonterminal{T}.SetProductions(ImmutableArray{IProduction{T}})"/>
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
        return Create(name, productions.ToImmutableArray());
    }

    /// <summary>
    /// Creates a nonterminal that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol<T> Create<T>(string name, ImmutableArray<IProduction<T>> productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        return new Nonterminal<T>(name, productions);
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value and whose productions
    /// must be assigned at a later time.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ProductionBuilder[])"/>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ImmutableArray{ProductionBuilder})"/>
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
        return CreateUntyped(name, productions.ToImmutableArray());
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions, represented as <see cref="ProductionBuilder"/>
    /// objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol CreateUntyped(string name, ImmutableArray<ProductionBuilder> productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        return new Untyped.Nonterminal(name, productions);
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
// Keep this synchronized with the untyped Nonterminal class at Untyped.cs.
public sealed class Nonterminal<T> : INonterminal, IGrammarSymbol<T>
{
    /// <summary>
    /// The nonterminal's productions.
    /// </summary>
    /// <remarks>
    /// Valid states:
    /// <list type="bullet">
    /// <item><description><see langword="default"/>: the productions have not been set yet</description></item>
    /// <item><description>empty the nonterminal has been used in building a grammar, but no productions had been set yet</description></item>
    /// <item><description>non-empty: the productions have been set</description></item>
    /// </list>
    /// </remarks>
    private ImmutableArray<IProduction<T>> _productions;

    /// <inheritdoc/>
    public string Name { get; }

    ISymbolBase IGrammarBuilder.Symbol => this;

    internal Nonterminal(string name, ImmutableArray<IProduction<T>> productions = default)
    {
        Name = name;
        _productions = productions;
    }

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
        SetProductions(productions.ToImmutableArray());
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">An immutable array with the productions to set.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(ImmutableArray<IProduction<T>> productions)
    {
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        if (!ImmutableInterlocked.InterlockedCompareExchange(ref _productions, productions, default).IsDefault)
        {
            ThrowHelpers.ThrowInvalidOperationExceptionLocalized(nameof(Resources.Builder_Nonterminal_SetProductionsManyTimes));
        }
    }

    ImmutableArray<IProduction> INonterminal.FreezeAndGetProductions()
    {
        ImmutableInterlocked.InterlockedCompareExchange(ref _productions, [], default);
        return _productions.CastArray<IProduction>();
    }
}
