// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using static Farkle.Builder.Regex;
using System;
using System.Globalization;
using System.Text.Json.Nodes;

// ReSharper disable once CheckNamespace
namespace Farkle.Samples.CSharp
{
    public static class JSON
    {
        private static JsonNode? ToDecimal(ReadOnlySpan<char> data)
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

        public static readonly DesigntimeFarkle<JsonNode?> Designtime;

        public static readonly RuntimeFarkle<JsonNode?> Runtime;

        static JSON()
        {
            var number = Terminal.Create("Number", (_, data) => ToDecimal(data),
                Join(
                    Literal('-').Optional(),
                    Literal('0').Or(OneOf("123456789").And(OneOf(PredefinedSets.Number).ZeroOrMore())),
                    Literal('.').And(OneOf(PredefinedSets.Number).AtLeast(1)).Optional(),
                    Join(
                        OneOf("eE"),
                        OneOf("+-").Optional(),
                        OneOf(PredefinedSets.Number).AtLeast(1)).Optional()));
            var jsonString = Terminals.StringEx("/bfnrt", true, false, '"', "String");
            var jsonObject = Nonterminal.Create<JsonObject>("Object");
            var jsonArray = Nonterminal.Create<JsonArray>("Array");
            var value = Nonterminal.Create("Value",
                jsonString.Finish(x => (JsonNode?)JsonValue.Create(x)),
                number.AsIs(),
                jsonObject.Finish(x => (JsonNode?)x),
                jsonArray.Finish(x => (JsonNode?)x),
                "true".Finish<JsonNode?>(() => JsonValue.Create(true)),
                "false".Finish<JsonNode?>(() => JsonValue.Create(false)),
                "null".FinishConstant<JsonNode?>(null));

            var arrayReversed = Nonterminal.Create<JsonArray>("Array Reversed");
            arrayReversed.SetProductions(
                arrayReversed.Extended().Append(",").Extend(value).Finish((xs, x) =>
                {
                    xs.Add(x);
                    return xs;
                }),
                value.Finish(x =>
                {
                    var xs = new JsonArray();
                    xs.Add(x);
                    return xs;
                }));
            var arrayOptional = Nonterminal.Create("Array Optional",
                arrayReversed.AsIs(),
                ProductionBuilder.Empty.Finish(() => new JsonArray()));
            jsonArray.SetProductions("[".Appended().Extend(arrayOptional).Append("]").AsIs());

            var objectElement = Nonterminal.Create<JsonObject>("Object Element");
            objectElement.SetProductions(
                objectElement.Extended().Append(",").Extend(jsonString).Append(":").Extend(value)
                    .Finish((xs, k, v) =>
                    {
                        xs.Add(k, v);
                        return xs;
                    }),
                jsonString.Extended().Append(":").Extend(value)
                    .Finish((k, v) =>
                    {
                        var xs = new JsonObject();
                        xs.Add(k, v);
                        return xs;
                    }));
            var objectOptional = Nonterminal.Create("Object Optional",
                objectElement.AsIs(),
                ProductionBuilder.Empty.Finish(() => new JsonObject()));
            jsonObject.SetProductions("{".Appended().Extend(objectOptional).Append("}").AsIs());

            Designtime = value.CaseSensitive().UseDynamicCodeGen();
            Runtime = Designtime.Build();
        }
    }
}
