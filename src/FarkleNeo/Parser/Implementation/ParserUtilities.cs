// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using System.Collections.Immutable;

namespace Farkle.Parser.Implementation;

internal static class ParserUtilities
{
    private static string GetAbbreviatedLexicalErrorText(ReadOnlySpan<char> chars)
    {
        const int MaxLength = 20;
        bool isAbbreviated = false;
        int eolIndex = chars.IndexOfAny('\n', '\r');
        if (eolIndex >= 0)
        {
            chars = chars[..eolIndex];
            isAbbreviated = true;
        }
        if (chars.Length > MaxLength)
        {
            chars = chars[..MaxLength];
            isAbbreviated = true;
        }
        if (!isAbbreviated)
        {
            return chars.ToString();
        }
#if NET6_0_OR_GREATER
        return $"{chars}…";
#else
        return $"{chars.ToString()}…";
#endif
    }

    public static unsafe string GetAbbreviatedLexicalErrorText<TChar>(ReadOnlySpan<TChar> chars)
    {
        if (typeof(TChar) == typeof(char))
        {
            return GetAbbreviatedLexicalErrorText(*(ReadOnlySpan<char>*)&chars);
        }
        throw new NotImplementedException();
    }

    public static ImmutableArray<string?> GetExpectedSymbols(Grammar grammar, LrState state)
    {
        var builder = ImmutableArray.CreateBuilder<string?>();
        foreach (var action in state.Actions)
        {
            TokenSymbol symbol = grammar.GetTokenSymbol(action.Key);
            // TODO: Add a test once we add the builder and can define hidden terminals.
            if ((symbol.Attributes & TokenSymbolAttributes.Hidden) != 0)
            {
                continue;
            }
            builder.Add(grammar.GetString(symbol.Name));
        }
        if (state.EndOfFileActions.Count > 0)
        {
            builder.Add(null);
        }
        return builder.ToImmutable();
    }
}
