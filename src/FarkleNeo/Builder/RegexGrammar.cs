// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Farkle.Diagnostics;
using Farkle.Parser;

namespace Farkle.Builder;

/// <summary>
/// Contains the language specification for string regexes.
/// </summary>
/// <seealso cref="Regex.FromRegexString(string)"/>
/// <seealso cref="Regex.FromRegexString(string, CompatibilityLevel)"/>
internal static class RegexGrammar
{
    private static CharParser<Regex> s_parser = null!;

    private static object? s_parserLock;

    private static bool s_parserInitialized;

    private static ParserApplicationException CreateLocalizedException(string resourceName) =>
        new(LocalizedDiagnostic.Create(resourceName));

    internal static CharParser<Regex> Parser =>
        LazyInitializer.EnsureInitialized(ref s_parser, ref s_parserInitialized, ref s_parserLock, () => GetGrammarBuilder().Build());

    private static int ParseInt(ReadOnlySpan<char> span)
    {
        try
        {
            return int.Parse(Terminals.ToCharacters(span));
        }
        catch (Exception ex)
        {
            // It's too much effort to get the precise position of
            // the error, and for too little benefit. If you write
            // x{999999999999999} sorry but the error position will
            // be one character off.
            throw new ParserApplicationException(ex.Message);
        }
    }

    /// <summary>
    /// Executes a regex quantifier and rethrows exceptions as
    /// <see cref="ParserApplicationException"/>.
    /// </summary>
    /// <remarks>
    /// We need this to protect from patterns that would throw in code-based regexes, like <c>x{4,2}</c>.
    /// </remarks>
    private static Func<Regex, Regex> ProtectQuantifier(Func<Regex, Regex> f) =>
        (Regex r) =>
        {
            try
            {
                return f(r);
            }
            catch (Exception ex)
            {
                throw new ParserApplicationException(ex.Message);
            }
        };

    /// <summary>
    /// Parses a character set expression into an array of character ranges.
    /// </summary>
    /// <param name="parserState">The state of the parsing operation.</param>
    /// <param name="chars">The character set expression.</param>
    /// <param name="startIndex">The index the content of the character set starts, or alternatively the length of the opening bracket.</param>
    /// <remarks>
    /// This method scans the characters from left to right using a state machine, so that
    /// character sequences of the form <c>a-z</c> are parsed into ranges of characters, while
    /// also allowing the use of escape sequences.
    /// </remarks>
    private static ImmutableArray<(char, char)> ParseCharacterSet(in ParserState parserState, ReadOnlySpan<char> chars, int startIndex)
    {
        // We trim only the end of the span but start parsing after the starting characters.
        // We keep them on the span for accurate error position reporting.
        chars = chars[..^1];
        // Optimistically reserve the maximum amount of characters. This will save an
        // allocation if the set consists of only single characters with no escaping.
        var builder = ImmutableArray.CreateBuilder<(char, char)>(chars.Length);
        var state = ParseCharacterSetState.Empty;
        char cPrevious = '\0';
        int i = startIndex;
        while (i < chars.Length)
        {
            switch (state, chars[i])
            {
                case (ParseCharacterSetState.Empty, '\\'):
                    state = ParseCharacterSetState.HasChar;
                    cPrevious = chars[i + 1];
                    i += 2;
                    break;
                case (ParseCharacterSetState.Empty, char c):
                    state = ParseCharacterSetState.HasChar;
                    cPrevious = c;
                    i++;
                    break;
                case (ParseCharacterSetState.HasChar, '\\'):
                    builder.Add((cPrevious, cPrevious));
                    // state remains the same.
                    cPrevious = chars[i + 1];
                    i += 2;
                    break;
                case (ParseCharacterSetState.HasChar, '-'):
                    state = ParseCharacterSetState.HasDash;
                    // cPrevious remains the same.
                    i++;
                    break;
                case (ParseCharacterSetState.HasChar, char c):
                    builder.Add((cPrevious, cPrevious));
                    // state remains the same.
                    cPrevious = c;
                    i++;
                    break;
                case (_, char c):
                    Debug.Assert(state == ParseCharacterSetState.HasDash);
                    char cTo = c == '\\' ? chars[i + 1] : c;
                    if (cPrevious <= cTo)
                    {
                        builder.Add((cPrevious, cTo));
                    }
                    else
                    {
                        var pos = parserState.GetPositionAfter(chars[..(i - 1)]);
                        object error = LocalizedDiagnostic.Create(nameof(Resources.Builder_RegexCharacterRangeReverseOrder));
                        error = new ParserDiagnostic(pos, error);
                        throw new ParserApplicationException(error);
                    }
                    state = ParseCharacterSetState.Empty;
                    cPrevious = '\0';
                    i += c == '\\' ? 2 : 1;
                    break;
            }
        }

        switch (state)
        {
            case ParseCharacterSetState.HasChar:
                builder.Add((cPrevious, cPrevious));
                break;
            case ParseCharacterSetState.HasDash:
                builder.Add((cPrevious, cPrevious));
                builder.Add(('-', '-'));
                break;
        }

        return builder.DrainToImmutable();
    }

    internal static IGrammarBuilder<Regex> GetGrammarBuilder()
    {
        var specialCharacters = "\\.[{()|?*+".ToImmutableArray();

        // For a moment this was matching as many characters as possible to create a
        // single string literal node, but this is not correct, because it matched
        // abc? as (abc)? instead of ab(c)?.
        var anyCharacter = Terminal.Create("Any character",
            Regex.NotOneOf(specialCharacters),
            (ref ParserState state, ReadOnlySpan<char> data) => Regex.Literal(data[0]));

        var escapedCharacter = Terminal.Create("Escaped character",
            Regex.Literal('\\') + Regex.OneOf(specialCharacters),
            (ref ParserState _, ReadOnlySpan<char> data) => Regex.Literal(data[1]));

        var predefinedSet = MakePredefinedSet("Predefined set (unsupported)", @"\p{");
        var allButPredefinedSet = MakePredefinedSet("All but Predefined set (unsupported)", @"\P{");
        var category = MakeCategory("Unicode category (unsupported)", @"\p");
        var allButCategory = MakeCategory("All but Unicode category (unsupported)", @"\P");
        var characterSet = MakeCharacterSet("Character set", "[", Regex.OneOf);
        var allButCharacterSet = MakeCharacterSet("All but Character set", "[^", Regex.NotOneOf);

        ImmutableArray<(char, char)> numbers = [('0', '9')];
        ImmutableArray<char> whitespace = [' ', '\t', '\n', '\r'];
        var numbersRegex = Regex.OneOf(numbers).AtLeast(1);

        var quantRepeat = Terminal.Create("Repeat quantifier",
            Regex.Join(Regex.Literal('{'), numbersRegex, Regex.Literal('}')),
            (ref ParserState _, ReadOnlySpan<char> data) =>
            {
                var count = ParseInt(data[1..^1]);
                return ProtectQuantifier((Regex r) => r.Repeat(count));
            });

        var quantAtLeast = Terminal.Create("At least quantifier",
            Regex.Join(Regex.Literal('{'), numbersRegex, Regex.Literal(",}")),
            (ref ParserState _, ReadOnlySpan<char> data) =>
            {
                var count = ParseInt(data[1..^2]);
                return ProtectQuantifier((Regex r) => r.AtLeast(count));
            });

        var quantBetween = Terminal.Create("Between quantifier",
            Regex.Join(Regex.Literal('{'), numbersRegex, Regex.Literal(','), numbersRegex, Regex.Literal('}')),
            (ref ParserState _, ReadOnlySpan<char> data) =>
            {
                data = data[1..^1];
                var commaPos = data.IndexOf(',');
                var numFrom = ParseInt(data[..commaPos]);
                var numTo = ParseInt(data[(commaPos + 1)..]);
                return ProtectQuantifier((Regex r) => r.Between(numFrom, numTo));
            });

        var regex = Nonterminal.Create<Regex>("Regex");

        var quantifier = Nonterminal.Create("Quantifier",
            "*".Appended().FinishConstant((Regex r) => r.ZeroOrMore()),
            "+".Appended().FinishConstant((Regex r) => r.AtLeast(1)),
            "?".Appended().FinishConstant((Regex r) => r.Optional()),
            quantRepeat.AsProduction(),
            quantAtLeast.AsProduction(),
            quantBetween.AsProduction()
        );

        var regexItem = Nonterminal.Create("Regex item",
            ".".Appended().FinishConstant(Regex.Any),
            "\\d".Appended().FinishConstant(Regex.OneOf(numbers)),
            "\\D".Appended().FinishConstant(Regex.NotOneOf(numbers)),
            "\\s".Appended().FinishConstant(Regex.OneOf(whitespace)),
            "\\S".Appended().FinishConstant(Regex.NotOneOf(whitespace)),
            anyCharacter.AsProduction(),
            escapedCharacter.AsProduction(),
            predefinedSet.AsProduction(),
            allButPredefinedSet.AsProduction(),
            category.AsProduction(),
            allButCategory.AsProduction(),
            characterSet.AsProduction(),
            allButCharacterSet.AsProduction(),
            "(".Appended().Extend(regex).Append(")").AsProduction()
        );

        var regexQuantified = Nonterminal.Create("Regex quantified",
            regexItem.AsProduction(),
            regexItem.Extended().Extend(quantifier).Finish((r, f) => f(r))
        );

        // We don't use an immutable array builder because it does not have a default constructor.
        var regexConcatenation =
            regexQuantified
            .Many<Regex, List<Regex>>(atLeastOnce: true)
            .Rename("Regex concatenation builder")
            // Do not replace with collection expressions, ToImmutableArray is optimized for Lists.
            .Select(x => Regex.Join(x.ToImmutableArray()))
            .Rename("Regex concatenation");

        var regexAlternationBuilder =
            regexConcatenation
            .SeparatedBy<Regex, List<Regex>>(Terminal.Literal("|"), atLeastOnce: true)
            .Rename("Regex alternation builder");

        regex.SetProductions(
            regexAlternationBuilder.Finish(x => Regex.Choice(x.ToImmutableArray()))
        );

        return regex.AutoWhitespace(false);

        static IGrammarSymbol<Regex> MakePredefinedSet(string name, string start) =>
            Terminal.Create<Regex>(name,
                Regex.Join(Regex.Literal(start), Regex.NotOneOf('{', '}').AtLeast(1), Regex.Literal('}')),
                (ref ParserState _, ReadOnlySpan<char> _) =>
                    throw CreateLocalizedException(nameof(Resources.Builder_RegexStringPredefinedSetsNotSupported)),
                TerminalOptions.Hidden);

        static IGrammarSymbol<Regex> MakeCategory(string name, string start) =>
            Terminal.Create<Regex>(name,
                // According to https://www.regular-expressions.info/unicode.html,
                // this syntax accepts only single-letter categories.
                Regex.Literal(start) + Regex.OneOf(('A', 'Z')),
                (ref ParserState _, ReadOnlySpan<char> _) =>
                    throw CreateLocalizedException(nameof(Resources.Builder_RegexStringUnicodeCategoriesNotSupported)),
                TerminalOptions.Hidden);

        static IGrammarSymbol<Regex> MakeCharacterSet(string name, string start, Func<ImmutableArray<(char, char)>, Regex> fChars)
        {
            // Should we also support shorthand escape sequences inside character sets like [\da-z]?
            // On first sight not, because it will increase complexity of the transformer, with minimal benefit.
            // When we support Unicode categories in the future, we can revisit this.
            Regex escapedChar = Regex.Literal('\\') + Regex.OneOf('\\', ']', '^', '-');
            return Terminal.Create(name,
                Regex.Join(
                    Regex.Literal(start),
                    // The first character can also be an unescaped closing bracket (making []] valid),
                    // but cannot be a caret in non-negated sets (to resolve the ambiguity with
                    // negated sets).
                    Regex.Choice(
                        escapedChar,
                        Regex.NotOneOf(start.EndsWith("^") ? ['\\'] : ['\\', '^'])
                    ),
                    // Subsequent characters can be anything except an unescaped closing bracket.
                    Regex.Choice(
                        escapedChar,
                        Regex.NotOneOf(['\\', ']'])
                    ).ZeroOrMore(),
                    Regex.Literal(']')
                ),
                (ref ParserState state, ReadOnlySpan<char> data) =>
                    fChars(ParseCharacterSet(in state, data, start.Length)));
        }
    }

    private enum ParseCharacterSetState
    {
        Empty,
        HasChar,
        HasDash
    }
}
