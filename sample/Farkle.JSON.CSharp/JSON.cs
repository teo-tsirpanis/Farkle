// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using static Chiron;
using Microsoft.FSharp.Collections;
using System;
using System.Globalization;
using System.Text;
using Farkle.CSharp;
using Farkle.Builder;
using static Farkle.Builder.Regex;

namespace Farkle.JSON.CSharp
{
    public static class Language
    {
        private static string UnescapeJsonString(ReadOnlySpan<char> data)
        {
            // Trim the initial and final double quotes
            data = data.Slice(1, data.Length - 2);
            var sb = new StringBuilder(data.Length);
            var i = 0;
            while (i < data.Length)
            {
                var ch = data[i++];
                if (ch != '\\')
                {
                    sb.Append(ch);
                }
                else
                {
                    ch = data[i++];
                    if (ch == '\"')
                        sb.Append('\"');
                    else if (ch == '\\')
                        sb.Append('\\');
                    else if (ch == '/')
                        sb.Append('/');
                    else if (ch == 'b')
                        sb.Append('\b');
                    else if (ch == 'f')
                        sb.Append('\f');
                    else if (ch == 'n')
                        sb.Append('\n');
                    else if (ch == 'r')
                        sb.Append('\r');
                    else if (ch == 't')
                        sb.Append('\t');
                    else if (ch == 'u')
                    {
                        var hexString =
#if NETCOREAPP2_1
                            data.Slice(i, 4);
#else
                            data.Slice(i, 4).ToString();
#endif
                        var hexChar = ushort.Parse(hexString, NumberStyles.HexNumber);
                        sb.Append((char) hexChar);
                        i += 4;
                    }
                }
            }

            return sb.ToString();
        }

        private static Json ToDecimal(ReadOnlySpan<char> data)
        {
            var num =
#if NETCOREAPP2_1
                decimal.Parse(data, NumberStyles.AllowExponent | NumberStyles.Float, CultureInfo.InvariantCulture);
#else
                decimal.Parse(data.ToString(), NumberStyles.AllowExponent | NumberStyles.Float,
                    CultureInfo.InvariantCulture);
#endif
            return Json.NewNumber(num);
        }

        public static readonly DesigntimeFarkle<Json> Designtime;

        public static readonly RuntimeFarkle<Json> Runtime;

        static Language()
        {
            var number = Terminal<Json>.Create("Number", (position, data) => ToDecimal(data),
                Join(
                    Literal('-').Optional(),
                    Literal('0').Or(OneOf("123456789").And(OneOf(PredefinedSets.Number).ZeroOrMore())),
                    Literal('.').And(OneOf(PredefinedSets.Number).AtLeast(1)),
                    Join(
                        OneOf("eE"),
                        OneOf("+-").Optional(),
                        OneOf(PredefinedSets.Number).AtLeast(1)).Optional()));
            var stringCharacters = PredefinedSets.AllValid.Characters.Remove('"').Remove('\\');
            var jsonString = Terminal<string>.Create("String", (position, data) => UnescapeJsonString(data),
                Join(
                    Literal('"'),
                    Choice(
                        OneOf(stringCharacters),
                        Join(
                            Literal('\\'),
                            Choice(
                                OneOf("\"\\/bfnrt"),
                                Literal('u').And(OneOf("1234567890ABCDEF").Repeat(4))))).ZeroOrMore(),
                    Literal('"')));
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
                arrayReversed.Extend().Append(",").Extend(value).Finish((xs, x) => FSharpList<Json>.Cons(x, xs)),
                value.Finish(ListModule.Singleton));
            var arrayOptional = Nonterminal.Create("Array Optional",
                arrayReversed.Finish(ListModule.Reverse),
                ProductionBuilder.Empty.FinishConstant(FSharpList<Json>.Empty));
            jsonArray.SetProductions("[".Append().Extend(arrayOptional).Append("]").Finish(Json.NewArray));

            var objectElement = Nonterminal.Create<FSharpList<Tuple<string, Json>>>("Object Element");
            objectElement.SetProductions(
                objectElement.Extend().Append(",").Extend(jsonString).Append(":").Extend(value)
                    .Finish((xs, k, v) => FSharpList<Tuple<string, Json>>.Cons(Tuple.Create(k, v), xs)),
                jsonString.Extend().Append(":").Extend(value).Finish((k, v) =>ListModule.Singleton(Tuple.Create(k, v))));
            var objectOptional = Nonterminal.Create("Object Optional",
                objectElement.Finish(x => Json.NewObject(MapModule.OfList(x))));
            jsonObject.SetProductions("{".Append().Extend(objectOptional).Append("}").AsIs());

            Designtime = value.CaseSensitive();
            Runtime = Designtime.Build();
        }
    }
}
