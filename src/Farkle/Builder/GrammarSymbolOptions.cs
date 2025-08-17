// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Builder;

/// <summary>
/// Contains additional configuration that applies to individual grammar symbols.
/// </summary>
/// <seealso cref="GrammarGlobalOptions"/>
internal readonly struct GrammarSymbolOptions
{
    // Any option added here has to clearly define its behavior when applied multiple times.
    // Unlike the global options, we cannot say "the last write wins", because the builder
    // can see multiple instances referring to the same symbol, each with its own options.
    // Saying "first import wins" is possible but messy. That's why operator scope was moved
    // to global options, and one of the reasons renaming was removed. Options like adding a
    // special name are fine, because their semantics are additive.

    public ImmutableList<string> SpecialNames { get; init; } = [];

    public GrammarSymbolOptions() { }

    public static readonly GrammarSymbolOptions Default = new();

    public GrammarSymbolOptions AddSpecialName(string specialName) =>
        this with { SpecialNames = SpecialNames.Add(specialName) };
}
