// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Diagnostics;

/// <summary>
/// Represents the name of a token symbol in a grammar to be built, along with diagnostic information.
/// </summary>
/// <param name="Name">The value of <see cref="Name"/>.</param>
/// <param name="Kind">The value of <see cref="Kind"/>.</param>
/// <param name="ShouldDisambiguate">The value of <see cref="ShouldDisambiguate"/>.</param>
internal readonly struct BuilderSymbolName(string Name, TokenSymbolKind Kind, bool ShouldDisambiguate) : IFormattable
#if NET6_0_OR_GREATER
    , ISpanFormattable
#endif
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

    public static implicit operator BuilderSymbolName((string Name, TokenSymbolKind Kind, bool ShouldDisambiguate) x) =>
        new(x.Name, x.Kind, x.ShouldDisambiguate);

    private static string GetTokenSymbolKindName(TokenSymbolKind kind, IFormatProvider? formatProvider)
    {
        return kind switch
        {
            TokenSymbolKind.Terminal => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_Terminal), formatProvider, "terminal"),
            TokenSymbolKind.Noise => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_Noise), formatProvider, "noise"),
            TokenSymbolKind.GroupStart => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_GroupStart), formatProvider, "group start"),
            TokenSymbolKind.GroupEnd => Resources.GetResourceString(nameof(Resources.Builder_SymbolKind_GroupEnd), formatProvider, "group end"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

#if NET6_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bool shouldQuote = Kind is TokenSymbolKind.Terminal;
        switch (shouldQuote, ShouldDisambiguate)
        {
            case (false, false):
                return destination.TryWrite(provider, $"({Name})", out charsWritten);
            case (false, true):
                return destination.TryWrite(provider, $"({Name}) ({GetTokenSymbolKindName(Kind, provider)})", out charsWritten);
            case (true, false):
                return destination.TryWrite(provider, $"({TokenSymbol.FormatName(Name)})", out charsWritten);
            case (true, true):
                return destination.TryWrite(provider, $"({TokenSymbol.FormatName(Name)}) ({GetTokenSymbolKindName(Kind, provider)})", out charsWritten);
        }
    }
#endif

    string IFormattable.ToString(string? format, IFormatProvider? provider)
    {
        bool shouldQuote = Kind is TokenSymbolKind.Terminal;
        switch (shouldQuote, ShouldDisambiguate)
        {
            case (false, false):
                return $"({Name})";
            case (false, true):
                return $"({Name}) ({GetTokenSymbolKindName(Kind, provider)})";
            case (true, false):
                return TokenSymbol.FormatName(Name);
            case (true, true):
                return $"{TokenSymbol.FormatName(Name)} ({GetTokenSymbolKindName(Kind, provider)})";
        }
    }
}
