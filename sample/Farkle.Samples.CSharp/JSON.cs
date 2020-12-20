// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using static Chiron;
using Microsoft.FSharp.Collections;
using System;
using System.Globalization;
using Farkle.Builder;
using static Farkle.Builder.Regex;

// ReSharper disable once CheckNamespace
namespace Farkle.Samples.CSharp
{
    public static class JSON
    {
        private static Json ToDecimal(ReadOnlySpan<char> data)
        {
            var data2 =
#if NETCOREAPP3_1
                data;
#else
                data.ToString();
#endif
            var num =
                decimal.Parse(data2, NumberStyles.AllowExponent | NumberStyles.Float, CultureInfo.InvariantCulture);
            return Json.NewNumber(num);
        }

        public static readonly DesigntimeFarkle<Json> Designtime;

        public static readonly RuntimeFarkle<Json> Runtime;

        static JSON()
        {
            var number = Terminal.Create("Number", (position, data) => ToDecimal(data),
                Join(
                    Literal('-').Optional(),
                    Literal('0').Or(OneOf("123456789").And(OneOf(PredefinedSets.Number).ZeroOrMore())),
                    Literal('.').And(OneOf(PredefinedSets.Number).AtLeast(1)).Optional(),
                    Join(
                        OneOf("eE"),
                        OneOf("+-").Optional(),
                        OneOf(PredefinedSets.Number).AtLeast(1)).Optional()));
            var jsonString = Terminals.StringEx("/bfnrt", true, false, '"', "String");
            var jsonObject = Nonterminal.Create<Json>("Object");
            var jsonArray = Nonterminal.Create<Json>("Array");
            var value = Nonterminal.Create("Value",
                jsonString.Finish(Json.NewString),
                number.AsIs(),
                jsonObject.AsIs(),
                jsonArray.AsIs(),
                "true".FinishConstant(Json.NewBool(true)),
                "false".FinishConstant(Json.NewBool(false)),
                "null".FinishConstant(Json.NewNull(null)));
            var arrayReversed = Nonterminal.Create<FSharpList<Json>>("Array Reversed");
            arrayReversed.SetProductions(
                arrayReversed.Extended().Append(",").Extend(value).Finish((xs, x) => FSharpList<Json>.Cons(x, xs)),
                value.Finish(ListModule.Singleton));
            var arrayOptional = Nonterminal.Create("Array Optional",
                arrayReversed.Finish(ListModule.Reverse),
                ProductionBuilder.Empty.FinishConstant(FSharpList<Json>.Empty));
            jsonArray.SetProductions("[".Appended().Extend(arrayOptional).Append("]").Finish(Json.NewArray));

            var objectElement = Nonterminal.Create<FSharpList<Tuple<string, Json>>>("Object Element");
            objectElement.SetProductions(
                objectElement.Extended().Append(",").Extend(jsonString).Append(":").Extend(value)
                    .Finish((xs, k, v) => FSharpList<Tuple<string, Json>>.Cons(Tuple.Create(k, v), xs)),
                jsonString.Extended().Append(":").Extend(value).Finish((k, v) =>ListModule.Singleton(Tuple.Create(k, v))));
            var objectOptional = Nonterminal.Create("Object Optional",
                objectElement.Finish(x => Json.NewObject(MapModule.OfList(x))),
                ProductionBuilder.Empty.FinishConstant(Json.NewObject(MapModule.Empty<string, Json>())));
            jsonObject.SetProductions("{".Appended().Extend(objectOptional).Append("}").AsIs());

            Designtime = value.CaseSensitive();
            Runtime = Designtime.Build();
        }
    }
}
