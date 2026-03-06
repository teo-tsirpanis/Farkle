// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using System.Collections.Immutable;
#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

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

    public static string GetAbbreviatedLexicalErrorText<TChar>(ReadOnlySpan<TChar> chars)
    {
        if (typeof(TChar) == typeof(char))
        {
            return GetAbbreviatedLexicalErrorText(Utilities.BitCastSpan<TChar, char>(chars));
        }
        ThrowHelpers.ThrowUnsupportedCharacterException();
        return null!;
    }

    public static ImmutableArray<string?> GetExpectedSymbols(Grammar grammar, LrState state)
    {
        var builder = ImmutableArray.CreateBuilder<string?>();
        foreach (var action in state.Actions)
        {
            TokenSymbolDefinition symbol = action.Key;
            // TODO: Add a test once we add the builder and can define hidden terminals.
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

    internal static object SupplyParserStateInfo(object diagnostic, Grammar grammar, LrStateMachine lr, int parserState)
    {
        return diagnostic switch
        {
            IParserStateInfoSupplier x => x.WithParserStateInfo(GetExpectedTokenNames(), parserState),
            ParserDiagnostic { Message: IParserStateInfoSupplier x, Position: var position } =>
                new ParserDiagnostic(position, x.WithParserStateInfo(GetExpectedTokenNames(), parserState)),
            _ => diagnostic
        };

        ImmutableArray<string?> GetExpectedTokenNames() => GetExpectedSymbols(grammar, lr[parserState]);
    }
}
