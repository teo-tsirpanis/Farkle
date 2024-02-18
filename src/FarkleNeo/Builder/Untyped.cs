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
// Keep this synchronized with the typed Nonterminal class at Nonterminal.cs.
public sealed class Nonterminal : INonterminal
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
    private ImmutableArray<ProductionBuilder> _productions;

    /// <inheritdoc/>
    public string Name { get; }

    ISymbolBase IGrammarBuilder.Symbol => this;

    internal Nonterminal(string name, ImmutableArray<ProductionBuilder> productions = default)
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
        SetProductions(productions.ToImmutableArray());
    }

    /// <summary>
    /// Sets the productions of this nonterminal.
    /// </summary>
    /// <param name="productions">An immutable array with the productions to set. The productions are represented
    /// as <see cref="ProductionBuilder"/> objects that have not been <c>Extend</c>ed or <c>Finish</c>ed.</param>
    /// <exception cref="ArgumentException"><paramref name="productions"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The productions have already been successfully set.</exception>
    /// <remarks>This function and its overloads must be called exactly once, and before the
    /// nonterminal is used in building a grammar.</remarks>
    public void SetProductions(ImmutableArray<ProductionBuilder> productions)
    {
        if (productions.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(productions));
        }
        if (productions.IsEmpty)
        {
            ThrowHelpers.ThrowArgumentException("Productions cannot be empty", nameof(productions));
        }
        if (!ImmutableInterlocked.InterlockedCompareExchange(ref _productions, productions, default).IsDefault)
        {
            throw new InvalidOperationException("Cannot set productions of a nonterminal more than once.");
        }
    }

    ImmutableArray<IProduction> INonterminal.FreezeAndGetProductions()
    {
        ImmutableInterlocked.InterlockedCompareExchange(ref _productions, [], default);
        return _productions.CastArray<IProduction>();
    }
}
