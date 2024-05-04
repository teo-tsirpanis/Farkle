// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Builder.Untyped;

/// <summary>
/// Represents a nonterminal symbol in a grammar to be built that does not produce a value,
/// and allows setting its productions after its creation.
/// </summary>
/// <remarks>
/// In Farkle, builder objects are usually immutable. This exception exists to support
/// defining recursive nonterminals.
/// </remarks>
public sealed class Nonterminal : INonterminal
{
    /// <summary>
    /// The nonterminal's productions.
    /// </summary>
    /// <remarks>
    /// Valid states:
    /// <list type="bullet">
    /// <item><description><see langword="default"/>: The productions have not been set yet.</description></item>
    /// <item><description>empty: The nonterminal has been used in building a grammar, but no productions had been
    /// set yet. The user is prevented from setting an empty production list.</description></item>
    /// <item><description>non-empty: The productions have been set.</description></item>
    /// </list>
    /// </remarks>
    private ImmutableArray<IProduction> _productions;

    /// <inheritdoc/>
    public string Name { get; }

    ISymbolBase IGrammarBuilder.Symbol => this;

    internal Nonterminal(string name, ImmutableArray<IProduction> productions = default)
    {
        Name = name;
        _productions = productions;
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">An array with the productions to set. The productions are represented
    /// as <see cref="ProductionBuilder"/> objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(params ProductionBuilder[] productions)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(productions);
        SetProductions(productions.AsSpan());
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">The productions to set. The productions are represented
    /// as <see cref="ProductionBuilder"/> objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(ReadOnlySpan<ProductionBuilder> productions) =>
        SetProductions(ImmutableArray<IProduction>.CastUp(productions.ToImmutableArray()));

    internal void SetProductions(ImmutableArray<IProduction> productions)
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

    internal ImmutableArray<IProduction> FreezeAndGetProductions()
    {
        // Avoid the interlocked operation if the productions have already been frozen.
        if (_productions is { IsDefault: false } productions)
        {
            return productions;
        }

        ImmutableInterlocked.InterlockedCompareExchange(ref _productions, [], default);
        return _productions;
    }

    ImmutableArray<IProduction> INonterminal.FreezeAndGetProductions() => FreezeAndGetProductions();
}
