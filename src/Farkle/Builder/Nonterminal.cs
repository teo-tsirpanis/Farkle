// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
        ArgumentNullException.ThrowIfNull(name);
        return new(name);
    }

    /// <summary>
    /// Creates a nonterminal that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the nonterminal will produce.</typeparam>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol<T> Create<T>(string name, params ImmutableArray<IProduction<T>> productions)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        return new Nonterminal<T>(name, ImmutableArray<IProduction>.CastUp(productions));
    }

    /// <inheritdoc cref="Create{T}(string, ImmutableArray{IProduction{T}})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static IGrammarSymbol<T> Create<T>(string name, params IProduction<T>[] productions) =>
        Create(name, productions.ToImmutableArrayChecked());

    /// <summary>
    /// Creates a nonterminal that does not produce a value and whose productions
    /// must be assigned at a later time.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ProductionBuilder[])"/>
    /// <seealso cref="Untyped.Nonterminal.SetProductions(ImmutableArray{ProductionBuilder})"/>
    public static Untyped.Nonterminal CreateUntyped(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new(name);
    }

    /// <summary>
    /// Creates a nonterminal that does not produce a value.
    /// </summary>
    /// <param name="name">The nonterminal's name.</param>
    /// <param name="productions">The nonterminal's productions, represented as <see cref="ProductionBuilder"/>
    /// objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    public static IGrammarSymbol CreateUntyped(string name, params ImmutableArray<ProductionBuilder> productions)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        return new Untyped.Nonterminal(name, ImmutableArray<IProduction>.CastUp(productions));
    }

    /// <inheritdoc cref="CreateUntyped(string, ImmutableArray{ProductionBuilder})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static IGrammarSymbol CreateUntyped(string name, params ProductionBuilder[] productions) =>
        CreateUntyped(name, productions.ToImmutableArrayChecked());
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
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public void SetProductions(params IProduction<T>[] productions)
    {
        ArgumentNullException.ThrowIfNull(productions);
        SetProductions(productions.ToImmutableArrayChecked());
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">The productions to set.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(params ImmutableArray<IProduction<T>> productions)
    {
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentExceptionLocalized(nameof(Resources.Builder_Nonterminal_EmptyProductions), nameof(productions));
        }
        _innerNonterminal.SetProductions(ImmutableArray<IProduction>.CastUp(productions));
    }

    ImmutableArray<IProduction> INonterminal.FreezeAndGetProductions() =>
        _innerNonterminal.FreezeAndGetProductions();
}
