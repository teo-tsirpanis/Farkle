// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

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
}
