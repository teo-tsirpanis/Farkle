// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Diagnostics.Builder;

/// <summary>
/// Represents the name of a token symbol in a grammar to be built, along with diagnostic information.
/// </summary>
/// <param name="Name">The value of <see cref="Name"/>.</param>
/// <param name="Kind">The value of <see cref="Kind"/>.</param>
/// <param name="ShouldDisambiguate">The value of <see cref="ShouldDisambiguate"/>.</param>
internal readonly struct BuilderSymbolName(string Name, TokenSymbolKind Kind, bool ShouldDisambiguate) : ISpanFormattable
{
    /// <summary>
    /// The token symbol's name.
    /// </summary>
    public string Name { get; } = Name;

    /// <summary>
    /// The token symbol's <see cref="TokenSymbolKind"/>.
    /// </summary>
    public TokenSymbolKind Kind { get; } = Kind;

    /// <summary>
    /// Whether the kind of the token symbol should be displayed because
    /// there is a token symbol with the same name and a different kind
    /// in the grammar.
    /// </summary>
    public bool ShouldDisambiguate { get; } = ShouldDisambiguate;

    private static string GetTokenSymbolKindName(TokenSymbolKind kind)
    {
        return kind switch
        {
            TokenSymbolKind.Terminal => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_Terminal), "terminal"),
            TokenSymbolKind.Noise => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_Noise), "noise"),
            TokenSymbolKind.GroupStart => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_GroupStart), "group start"),
            TokenSymbolKind.GroupEnd => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_GroupEnd), "group end"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bool shouldQuote = Kind is TokenSymbolKind.Terminal;
        switch (shouldQuote, ShouldDisambiguate)
        {
            case (false, false):
                return destination.TryWrite(provider, $"({Name})", out charsWritten);
            case (false, true):
                return destination.TryWrite(provider, $"({Name}) ({GetTokenSymbolKindName(Kind)})", out charsWritten);
            case (true, false):
                return destination.TryWrite(provider, $"({TokenSymbolDefinition.FormatName(Name)})", out charsWritten);
            case (true, true):
                return destination.TryWrite(provider, $"({TokenSymbolDefinition.FormatName(Name)}) ({GetTokenSymbolKindName(Kind)})", out charsWritten);
        }
    }

    string IFormattable.ToString(string? format, IFormatProvider? provider)
    {
        bool shouldQuote = Kind is TokenSymbolKind.Terminal;
        switch (shouldQuote, ShouldDisambiguate)
        {
            case (false, false):
                return $"({Name})";
            case (false, true):
                return $"({Name}) ({GetTokenSymbolKindName(Kind)})";
            case (true, false):
                return TokenSymbolDefinition.FormatName(Name);
            case (true, true):
                return $"{TokenSymbolDefinition.FormatName(Name)} ({GetTokenSymbolKindName(Kind)})";
        }
    }
}
