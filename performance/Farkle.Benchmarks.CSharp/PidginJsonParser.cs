// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Originally copied from
// https://github.com/benjamin-hodgson/Pidgin/blob/main/Pidgin.Examples/Json/JsonParser.cs
// and extended to parse the full JSON grammar.

using System.Text.Json.Nodes;
using Pidgin;
using static Pidgin.Parser;

namespace Farkle.Benchmarks;

public static class PidginJsonParser
{
    private static readonly Parser<char, char> _comma = Char(',');

    private static readonly Parser<char, char> _charOrEscape =
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
                    Char('u').Then(_ =>
                        OneOf("0123456789abcdefABCDEF")
                            .RepeatString(4)
                            .Select(cs => (char)ushort.Parse(cs, System.Globalization.NumberStyles.HexNumber))
                    ))));

    private static readonly Parser<char, string> _string =
        _charOrEscape
            .ManyString()
            .Between(Char('"'));

    private static readonly Parser<char, JsonNode?> _jsonNumber =
        Real.Select<JsonNode?>(s => JsonValue.Create(s));

    private static readonly Parser<char, JsonNode?> _jsonString =
        _string.Select<JsonNode?>(s => JsonValue.Create(s));

    private static readonly Parser<char, JsonNode?> _json =
        Rec(() =>
            _jsonString
                .Or(_jsonNumber)
                .Or(_jsonArray!)
                .Or(_jsonObject!)
                .Or(String("true").Select<JsonNode?>(_ => JsonValue.Create(true)))
                .Or(String("false").Select<JsonNode?>(_ => JsonValue.Create(false)))
                .Or(String("null").ThenReturn<JsonNode?>(null))
            );

    private static readonly Parser<char, JsonNode?> _jsonArray =
        _json.Between(SkipWhitespaces)
            .Separated(_comma)
            .Between(Char('['), Char(']'))
            .Select<JsonNode?>(els =>
            {
                var array = new JsonArray();
                foreach (var el in els)
                {
                    array.Add(el);
                }

                return array;
            });

    private static readonly Parser<char, KeyValuePair<string, JsonNode?>> _jsonMember =
        _string
            .Before(Char(':').Between(SkipWhitespaces))
            .Then(_json, (name, val) => new KeyValuePair<string, JsonNode?>(name, val));

    private static readonly Parser<char, JsonNode?> _jsonObject =
        _jsonMember.Between(SkipWhitespaces)
            .Separated(_comma)
            .Between(Char('{'), Char('}'))
            .Select<JsonNode?>(xs => new JsonObject(xs));

    public static Result<char, JsonNode?> Parse(string input) => _json.Parse(input);

    public static Result<char, JsonNode?> Parse(TextReader input) => _json.Parse(input);
}
