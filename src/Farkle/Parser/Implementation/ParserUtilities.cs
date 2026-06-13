// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
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
        return $"{chars}…";
    }

    public static string GetAbbreviatedLexicalErrorText<TChar>(ReadOnlySpan<TChar> chars)
    {
        if (typeof(TChar) == typeof(char))
        {
            return GetAbbreviatedLexicalErrorText(Utilities.BitCastSpan<TChar, char>(chars));
        }
        ThrowHelpers.ThrowUnsupportedCharacterException();
        return null;
    }

    public static ImmutableArray<string?> GetExpectedSymbols(LrState state)
    {
        var builder = ImmutableArray.CreateBuilder<string?>();
        foreach (var action in state.Actions)
        {
            TokenSymbolDefinition symbol = action.Key;
            if ((symbol.Attributes & TokenSymbolAttributes.Hidden) != 0)
            {
                continue;
            }
            builder.Add(symbol.Name);
        }
        if (state.EndOfFileActions.Count > 0)
        {
            builder.Add(null);
        }
        return builder.ToImmutable();
    }

    internal static object SupplyParserStateInfo(object diagnostic, LrState state)
    {
        return diagnostic switch
        {
            IParserStateInfoSupplier x => x.WithParserStateInfo(GetExpectedSymbols(state), state.StateIndex),
            ParserDiagnostic { Message: IParserStateInfoSupplier x, Position: var position } =>
                new ParserDiagnostic(position, x.WithParserStateInfo(GetExpectedSymbols(state), state.StateIndex)),
            _ => diagnostic
        };
    }
}
