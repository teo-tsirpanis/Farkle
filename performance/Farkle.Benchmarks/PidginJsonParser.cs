// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Originally copied from
// https://github.com/benjamin-hodgson/Pidgin/blob/main/Pidgin.Examples/Json/JsonParser.cs
// and extended to parse the full JSON grammar and remove semantic actions.

using Pidgin;
using static Pidgin.Parser;

namespace Farkle.Benchmarks;

public static class PidginJsonParser
{
    private static readonly Parser<char, char> _comma = Char(',');

    private static readonly Parser<char, Unit> _charOrEscape =
        AnyCharExcept('"', '\\')
            .Or(Char('\\').Then(OneOf(
                    Char('"'),
                    Char('\\'),
                    Char('/'),
                    Char('b').ThenReturn('\b'),
                    Char('f').ThenReturn('\f'),
                    Char('n').ThenReturn('\n'),
                    Char('r').ThenReturn('\r'),
                    Char('t').ThenReturn('\t'),
                    Char('u').Then(OneOf("0123456789abcdefABCDEF").SkipRepeat(4))
                    )))
            .IgnoreResult();

    private static readonly Parser<char, Unit> _string =
        _charOrEscape
            .SkipMany()
            .Between(Char('"'));

    private static readonly Parser<char, Unit> _json =
        Rec(() => OneOf(
                _string,
                Real.IgnoreResult(),
                _jsonArray!,
                _jsonObject!,
                String("true").IgnoreResult(),
                String("false").IgnoreResult(),
                String("null").IgnoreResult()
            ));

    private static readonly Parser<char, Unit> _jsonArray =
        _json.Between(SkipWhitespaces)
            .SkipSeparated(_comma)
            .Between(Char('['), Char(']'));

    private static readonly Parser<char, Unit> _jsonMember =
        _string
            .Then(Char(':').Between(SkipWhitespaces))
            .Then(_json);

    private static readonly Parser<char, Unit> _jsonObject =
        _jsonMember.Between(SkipWhitespaces)
            .SkipSeparated(_comma)
            .Between(Char('{'), Char('}'));

    public static Result<char, Unit> Parse(string input) => _json.Parse(input);

    public static Result<char, Unit> Parse(TextReader input) => _json.Parse(input);

    private static Parser<TToken, T> SkipRepeat<TToken, T>(this Parser<TToken, T> parser, int count) =>
        Enumerable.Repeat(parser, count).Aggregate((a, b) => a.Then(b));

    private static Parser<TToken, Unit> SkipSeparated<TToken, T, U>(this Parser<TToken, T> parser,
        Parser<TToken, U> separator) =>
        new SkipSeparatedAtLeastOnceParser<TToken, T, U>(parser, separator)
            .Or(Parser<TToken>.Return(Unit.Value));

    /// <summary>
    /// Copy of Pidgin's <c>SeparatedAtLeastOnceParser</c> class, adapted to not return a value.
    /// </summary>
    /// <remarks>
    /// Pidgin does not have <c>SkipSeparated***</c> APIs, but Farkle's parsers consistently do
    /// not perform any semantic analysis, and we want to make the comparison fair.
    /// </remarks>
    private sealed class SkipSeparatedAtLeastOnceParser<TToken, T, U>(
        Parser<TToken, T> parser,
        Parser<TToken, U> separator) : Parser<TToken, Unit>
    {
        private readonly Parser<TToken, T> _remainderParser = separator.Then(parser);

        public override bool TryParse(ref ParseState<TToken> state, ref PooledList<Expected<TToken>> expecteds, out Unit result)
        {
            result = Unit.Value;
            return parser.TryParse(ref state, ref expecteds, out _) &&
                   Rest(_remainderParser, ref state, ref expecteds);
        }

        private static bool Rest(Parser<TToken, T> parser, ref ParseState<TToken> state, ref PooledList<Expected<TToken>> expecteds)
        {
            var lastStartingLoc = state.Location;
            var childExpecteds = new PooledList<Expected<TToken>>(state.Configuration.ArrayPoolProvider.GetArrayPool<Expected<TToken>>());
            while (parser.TryParse(ref state, ref childExpecteds, out _))
            {
                var endingLoc = state.Location;
                childExpecteds.Clear();

                if (endingLoc <= lastStartingLoc)
                {
                    childExpecteds.Dispose();
                    throw new InvalidOperationException("Many() used with a parser which consumed no input");
                }

                lastStartingLoc = endingLoc;
            }

            var lastParserConsumedInput = state.Location > lastStartingLoc;
            if (lastParserConsumedInput)
            {
                expecteds.AddRange(childExpecteds.AsSpan());
            }

            childExpecteds.Dispose();

            // we fail if the most recent parser failed after consuming input.
            // it sets state.Error for us
            return !lastParserConsumedInput;
        }
    }
}
