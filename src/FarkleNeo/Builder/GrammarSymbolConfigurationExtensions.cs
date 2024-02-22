// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

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
public static class GrammarSymbolConfigurationExtensions
{
    /// <summary>
    /// Renames a grammar symbol.
    /// </summary>
    /// <param name="symbol">The symbol to rename.</param>
    /// <param name="name">The symbol's new name.</param>
    /// <remarks>
    /// <para>
    /// If a grammar contains the same symbol both in unrenamed and renamed form,
    /// the symbol will be added once, with the renamed name. If a symbol is renamed
    /// multiple times within a grammar, the name of the symbol in the grammar is
    /// unspecified and a warning will be emitted by the builder.
    /// </para>
    /// <para>
    /// For the purposes of the previous paragraph, a symbol is considered renamed
    /// even if this function is called with the same name as the symbol's existing
    /// name.
    /// </para>
    /// </remarks>
    /// <seealso cref="IGrammarSymbol.Name"/>
    public static IGrammarSymbol Rename(this IGrammarSymbol symbol, string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        Debug.Assert(symbol is GrammarSymbolWrapper or ISymbolBase);
        if (symbol is GrammarSymbolWrapper wrapper)
            return wrapper.Rename(name);
        return new GrammarSymbolWrapper(name, (ISymbolBase)symbol);
    }

    /// <inheritdoc cref="Rename"/>
    public static IGrammarSymbol<T> Rename<T>(this IGrammarSymbol<T> symbol, string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        Debug.Assert(symbol is GrammarSymbolWrapper<T> or ISymbolBase);
        if (symbol is GrammarSymbolWrapper<T> wrapper)
            return wrapper.Rename(name);
        return new GrammarSymbolWrapper<T>(name, (ISymbolBase)symbol);
    }
}
