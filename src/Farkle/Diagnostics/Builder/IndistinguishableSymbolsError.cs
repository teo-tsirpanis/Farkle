// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;

namespace Farkle.Diagnostics.Builder;

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
public sealed class IndistinguishableSymbolsError : ISpanFormattable
{
    private ImmutableArray<(TokenSymbolKind, bool ShouldDisambiguate)> SymbolDiagnosticInfo { get; }

    /// <summary>
    /// The names of the conflicting symbols.
    /// </summary>
    public ImmutableArray<string> SymbolNames { get; }

    /// <summary>
    /// An example word that leads to the conflict between the symbols in <see cref="SymbolNames"/>.
    /// </summary>
    public string ExampleWord { get; }

    internal static IEnumerable<IndistinguishableSymbolsError> GetErrors(Grammar grammar, ISymbolNameProvider? symbolNameProvider)
    {
        if (grammar.DfaOnChar is { HasConflicts: true } dfa)
        {
            Debug.Assert(symbolNameProvider is not null);
            foreach (var error in GetErrors(dfa, symbolNameProvider))
            {
                yield return error;
            }
        }
    }

    internal static IEnumerable<IndistinguishableSymbolsError> GetErrors(Dfa<char> dfa, ISymbolNameProvider symbolNameProvider)
    {
        var wordGenerator = new DfaWordGenerator<char>(dfa);
        var seenConflicts = new HashSet<int>(new DfaAcceptSymbolComparer<char>(dfa));
        for (int i = 0; i < dfa.Count; i++)
        {
            var state = dfa[i];
            if (!state.HasConflicts)
            {
                continue;
            }
            // Do not log the same set of indistinguishable symbols twice.
            if (!seenConflicts.Add(i))
            {
                continue;
            }
            var exampleWord = wordGenerator.GenerateWordAsString(i);
            if (exampleWord is null)
            {
                continue;
            }
            int count = state.AcceptSymbols.Count;
            var namesBuilder = ImmutableArray.CreateBuilder<string>(count);
            var symbolInfoBuilder = ImmutableArray.CreateBuilder<(TokenSymbolKind, bool ShouldDisambiguate)>(count);
            foreach (var acceptSymbol in state.AcceptSymbols)
            {
                var name = symbolNameProvider.GetName(acceptSymbol.Handle);
                namesBuilder.Add(name.Name);
                symbolInfoBuilder.Add((name.Kind, name.ShouldDisambiguate));
            }
            yield return new IndistinguishableSymbolsError(namesBuilder.MoveToImmutable(), symbolInfoBuilder.MoveToImmutable(), exampleWord);
        }
    }

    internal IndistinguishableSymbolsError(ImmutableArray<string> symbolNames, ImmutableArray<(TokenSymbolKind, bool ShouldDisambiguate)> symbolDiagnosticInfo, string exampleWord)
    {
        if (symbolDiagnosticInfo.Length != symbolNames.Length)
        {
            throw new ArgumentException("Symbol name and diagnostic info arrays do not have the same length", nameof(symbolDiagnosticInfo));
        }
        Debug.Assert(symbolDiagnosticInfo.Length == symbolNames.Length);
        SymbolNames = symbolNames;
        SymbolDiagnosticInfo = symbolDiagnosticInfo;
        ExampleWord = exampleWord;
    }

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Builder_IndistinguishableSymbols), new DelimitedSymbolNames(this), ExampleWord);

    string IFormattable.ToString(string? format, IFormatProvider? provider) => ToString(provider);

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Resources.TryWrite(destination, provider, nameof(Resources.Builder_IndistinguishableSymbols), out charsWritten, new DelimitedSymbolNames(this), ExampleWord);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);

    [ExcludeFromCodeCoverage(Justification = "Diagnostics-only code")]
    private readonly struct DelimitedSymbolNames(IndistinguishableSymbolsError error) : ISpanFormattable
    {
        public IndistinguishableSymbolsError Error { get; } = error;

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
                shouldAppend = sb.AppendFormatted(new BuilderSymbolName(name, kind, shouldDisambiguate));
            }
            return destination.TryWrite(provider, ref sb, out charsWritten);
        }

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
                sb.Append(provider, $"{new BuilderSymbolName(name, kind, shouldDisambiguate)}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Compares two state indices of a <see cref="Dfa{TChar}"/> to determine if they have the same accept symbols.
    /// </summary>
    private sealed class DfaAcceptSymbolComparer<TChar>(Dfa<TChar> dfa) : IEqualityComparer<int>
    {
        public bool Equals(int x, int y)
        {
            var acceptSymbolsX = dfa[x].AcceptSymbols;
            var acceptSymbolsY = dfa[y].AcceptSymbols;
            if (acceptSymbolsX.Count != acceptSymbolsY.Count)
            {
                return false;
            }
            var iterX = acceptSymbolsX.GetEnumerator();
            var iterY = acceptSymbolsY.GetEnumerator();
            while (iterX.MoveNext() && iterY.MoveNext())
            {
                // The accept symbols are sorted by index.
                if (iterX.Current.Handle != iterY.Current.Handle)
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(int obj)
        {
            var acceptSymbols = dfa[obj].AcceptSymbols;
            var hc = new HashCode();
            hc.Add(acceptSymbols.Count);
            foreach (var symbol in acceptSymbols)
            {
                hc.Add(symbol.Handle);
            }
            return hc.ToHashCode();
        }
    }
}
