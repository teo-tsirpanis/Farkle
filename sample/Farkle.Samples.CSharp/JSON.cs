// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using static Farkle.Builder.Regex;
using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using Farkle.Parser;

// ReSharper disable once CheckNamespace
namespace Farkle.Samples.CSharp
{
    public static class JSON
    {
        private static JsonValue ToDecimal(ReadOnlySpan<char> data)
        {
            var data2 =
#if NETCOREAPP
                data;
#else
                data.ToString();
#endif
            var num =
                decimal.Parse(data2, NumberStyles.AllowExponent | NumberStyles.Float, CultureInfo.InvariantCulture);
            return JsonValue.Create(num);
        }

        public static readonly IGrammarBuilder<JsonNode?> Builder;

        public static readonly CharParser<JsonNode?> Parser;

        static JSON()
        {
            var number = Terminal.Create("Number",
                Join([
                    Literal('-').Optional(),
                    Literal('0') | OneOf("123456789") + OneOf("0123456789").ZeroOrMore(),
                    (Literal('.') + OneOf("0123456789").AtLeast(1)).Optional(),
                    Join([
                        OneOf("eE"),
                        OneOf("+-").Optional(),
                        OneOf("0123456789").AtLeast(1)]).Optional()]),
                 (ref ParserState _, ReadOnlySpan<char> data) => ToDecimal(data));
            var jsonString = Terminals.String("String", '"', "/bfnrtu", false);
            var jsonObject = Nonterminal.Create<JsonObject>("Object");
            var jsonArray = Nonterminal.Create<JsonArray>("Array");
            var value = Nonterminal.Create("Value",
                jsonString.Finish(x => JsonValue.Create(x)),
                number.AsProduction(),
                jsonObject.AsProduction(),
                jsonArray.AsProduction(),
                "true".Finish(() => JsonValue.Create(true)),
                "false".Finish(() => JsonValue.Create(false)),
                "null".FinishConstant((JsonNode?)null));

            var arrayReversed = Nonterminal.Create<JsonArray>("Array Reversed");
            arrayReversed.SetProductions(
                arrayReversed.Extended().Append(",").Extend(value).Finish((xs, x) =>
                {
                    xs.Add(x);
                    return xs;
                }),
                value.Finish(x => new JsonArray { x }));
            var arrayOptional = Nonterminal.Create("Array Optional",
                arrayReversed.AsProduction(),
                ProductionBuilder.Empty.Finish(() => new JsonArray()));
            jsonArray.SetProductions("[".Appended().Extend(arrayOptional).Append("]").AsProduction());

            var objectElement = Nonterminal.Create<JsonObject>("Object Element");
            objectElement.SetProductions(
                objectElement.Extended().Append(",").Extend(jsonString).Append(":").Extend(value)
                    .Finish((xs, k, v) =>
                    {
                        xs.Add(k, v);
                        return xs;
                    }),
                jsonString.Extended().Append(":").Extend(value)
                    .Finish((k, v) => new JsonObject { { k, v } }));
            var objectOptional = Nonterminal.Create("Object Optional",
                objectElement.AsProduction(),
                ProductionBuilder.Empty.Finish(() => new JsonObject()));
            jsonObject.SetProductions("{".Appended().Extend(objectOptional).Append("}").AsProduction());

            Builder = value.CaseSensitive(true);
            Parser = Builder.Build();

            static Regex OneOf(string chars) => Regex.OneOf(chars.ToImmutableArray());
        }
    }
}
