// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Farkle.Grammars;

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a case where the builder could
/// not distinguish between two or more symbols.
/// </summary>
/// <remarks>
/// The error messages of this class support disambiguating symbols
/// by their kind (terminal, noise, group start, group end) if the
/// same name appears in symbols of different kind.
/// </remarks>
/// <seealso href="https://github.com/teo-tsirpanis/Farkle/blob/mainstream/docs/diagnostics/FARKLE0002.md"/>
public sealed class IndistinguishableSymbolsError : IFormattable
#if NET8_0_OR_GREATER
    , ISpanFormattable
#endif
{
    private ImmutableArray<(TokenSymbolKind, bool ShouldDisambiguate)> SymbolDiagnosticInfo { get; }

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

    /// <summary>
    /// The names of the conflicting symbols.
    /// </summary>
    public ImmutableArray<string> SymbolNames { get; }

    internal IndistinguishableSymbolsError(ImmutableArray<string> symbolNames, ImmutableArray<(TokenSymbolKind, bool ShouldDisambiguate)> symbolDiagnosticInfo)
    {
        if (symbolDiagnosticInfo.Length != symbolNames.Length)
        {
            throw new ArgumentException("Symbol names and diagnostic info arrays do not have the same length", nameof(symbolDiagnosticInfo));
        }
        Debug.Assert(symbolDiagnosticInfo.Length == symbolNames.Length);
        SymbolNames = symbolNames;
        SymbolDiagnosticInfo = symbolDiagnosticInfo;
    }

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Builder_IndistinguishableSymbols), new DelimitedSymbolNames(this));

    string IFormattable.ToString(string? format, IFormatProvider? provider) => ToString(provider);

#if NET8_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Resources.TryWrite(destination, provider, nameof(Resources.Builder_IndistinguishableSymbols), out charsWritten, new DelimitedSymbolNames(this));
#endif

    /// <inheritdoc/>
    public override string ToString() => ToString(null);

    [ExcludeFromCodeCoverage(
#if NET5_0_OR_GREATER
        Justification = "Diagnostics-only code"
#endif
    )]
    private readonly struct DelimitedSymbolNames(IndistinguishableSymbolsError error) : IFormattable
#if NET6_0_OR_GREATER
        , ISpanFormattable
#endif
    {
        public IndistinguishableSymbolsError Error { get; } = error;

#if NET6_0_OR_GREATER
        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            // The counts don't matter much with this handler anyway.
            MemoryExtensions.TryWriteInterpolatedStringHandler sb = new(0, 0, destination, provider, out bool shouldAppend);
            if (!shouldAppend)
            {
                charsWritten = 0;
                return false;
            }
            bool first = true;
            var names = Error.SymbolNames.GetEnumerator();
            var info = Error.SymbolDiagnosticInfo.GetEnumerator();
            while (shouldAppend && names.MoveNext() && info.MoveNext())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    shouldAppend = sb.AppendLiteral(", ");
                    if (!shouldAppend) break;
                }
                string name = names.Current;
                (TokenSymbolKind kind, bool shouldDisambiguate) = info.Current;
                bool shouldQuote = kind is TokenSymbolKind.Terminal;
                switch (shouldQuote, shouldDisambiguate)
                {
                    case (false, false):
                        shouldAppend = sb.AppendLiteral("(");
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendFormatted(name);
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendLiteral(")");
                        break;
                    case (false, true):
                        shouldAppend = sb.AppendLiteral("(");
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendFormatted(name);
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendLiteral(") (");
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendFormatted(GetTokenSymbolKindName(kind, provider));
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendLiteral(")");
                        break;
                    case (true, false):
                        shouldAppend = sb.AppendFormatted(TokenSymbol.FormatName(name));
                        break;
                    case (true, true):
                        shouldAppend = sb.AppendFormatted(TokenSymbol.FormatName(name));
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendLiteral(" (");
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendFormatted(GetTokenSymbolKindName(kind, provider));
                        if (!shouldAppend) break;
                        shouldAppend = sb.AppendLiteral(")");
                        break;
                }
            }
            return destination.TryWrite(provider, ref sb, out charsWritten);
        }
#endif

        string IFormattable.ToString(string? format, IFormatProvider? provider)
        {
            StringBuilder sb = new();
            bool first = true;
            var names = Error.SymbolNames.GetEnumerator();
            var info = Error.SymbolDiagnosticInfo.GetEnumerator();
            while (names.MoveNext() && info.MoveNext())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(", ");
                }
                string name = names.Current;
                (TokenSymbolKind kind, bool shouldDisambiguate) = info.Current;
                bool shouldQuote = kind is TokenSymbolKind.Terminal;
                switch (shouldQuote, shouldDisambiguate)
                {
                    case (false, false):
                        sb.Append($"({name})");
                        break;
                    case (false, true):
                        sb.Append($"({name}) ({GetTokenSymbolKindName(kind, provider)})");
                        break;
                    case (true, false):
                        sb.Append(TokenSymbol.FormatName(name));
                        break;
                    case (true, true):
                        sb.Append($"{TokenSymbol.FormatName(name)} ({GetTokenSymbolKindName(kind, provider)})");
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
