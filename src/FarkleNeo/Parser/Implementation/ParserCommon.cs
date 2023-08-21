// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using System.Collections.Immutable;

namespace Farkle.Parser.Implementation;

internal static class ParserCommon
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
        int count = state.Actions.Count + (state.EndOfFileActions.Count > 0 ? 1 : 0);
        var builder = ImmutableArray.CreateBuilder<string?>(count);
        foreach (var action in state.Actions)
        {
            builder.Add(grammar.GetString(grammar.GetTokenSymbol(action.Key).Name));
        }
        if (state.EndOfFileActions.Count > 0)
        {
            builder.Add(null);
        }
        return builder.MoveToImmutable();
    }
}
