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
using Farkle.JSON.CSharp.Definitions;

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
                        var hexCode =
#if NETCOREAPP2_1
                            ushort.Parse(data.Slice(i, 4), NumberStyles.HexNumber);
#else
                            ushort.Parse(data.Slice(i, 4).ToString(), NumberStyles.HexNumber);
#endif
                        sb.Append((char) hexCode);
                        i += 4;
                    }
                }
            }

            return sb.ToString();
        }

        // This function converts terminals to anything you want.
        // If you do not care about a terminal (like single characters),
        // you can let the default case return null.
        private static object Transform(uint terminal, Position position, ReadOnlySpan<char> data)
        {
            switch ((Terminal) terminal)
            {
                case Terminal.Number:
                    var num =
#if NETCOREAPP2_1
                        decimal.Parse(data, NumberStyles.AllowExponent | NumberStyles.Float, CultureInfo.InvariantCulture);
#else
                        decimal.Parse(data.ToString(), NumberStyles.AllowExponent | NumberStyles.Float, CultureInfo.InvariantCulture);
#endif
                    // Avoid boxing by wrapping directly to the Json type.
                    return Json.NewNumber(num);
                case Terminal.String:
                    return UnescapeJsonString(data);
                default: return null;
            }
        }

        // The fusers merge the parts of a production into one object of your desire.
        // This function maps each production to a fuser.
        // Do not delete anything here, or the post-processor will fail.
        private static Fuser GetFuser(uint prod)
        {
            switch ((Production) prod)
            {
                case Production.ValueString:
                    return Fuser.Create<string, Json>(0, Json.NewString);
                case Production.ValueNumber:
                    return Fuser.First;
                case Production.ValueObject:
                    return Fuser.First;
                case Production.ValueArray:
                    return Fuser.First;
                case Production.ValueTrue:
                    return Fuser.Constant(Json.NewBool(true));
                case Production.ValueFalse:
                    return Fuser.Constant(Json.NewBool(false));
                case Production.ValueNull:
                    return Fuser.Constant(Json.NewNull(null));
                case Production.ArrayLBracketRBracket:
                    return Fuser.Create<FSharpList<Json>, Json>(1, Json.NewArray);
                case Production.ArrayOptionalArrayReversed:
                    return Fuser.Create<FSharpList<Json>, FSharpList<Json>>(0, ListModule.Reverse);
                case Production.ArrayOptionalEmpty:
                    return Fuser.Constant(FSharpList<Json>.Empty);
                case Production.ArrayReversedComma:
                    return Fuser.Create<Json, FSharpList<Json>, FSharpList<Json>>(2, 0, FSharpList<Json>.Cons);
                case Production.ArrayReversed:
                    return Fuser.Create<Json, FSharpList<Json>>(0, ListModule.Singleton);
                case Production.ObjectLBraceRBrace:
                    return Fuser.Create<FSharpList<Tuple<string, Json>>, Json>(1,
                        list => Json.NewObject(MapModule.OfList(list)));
                case Production.ObjectOptionalObjectElement:
                    return Fuser.First;
                case Production.ObjectOptionalEmpty:
                    return Fuser.Constant(FSharpList<Tuple<string, Json>>.Empty);
                case Production.ObjectElementCommaStringColon:
                    return Fuser.Create<string, Json, FSharpList<Tuple<string, Json>>, FSharpList<Tuple<string, Json>>>(
                        2, 4, 0,
                        (key, value, map) =>
                            FSharpList<Tuple<string, Json>>.Cons(new Tuple<string, Json>(key, value), map));
                case Production.ObjectElementStringColon:
                    return Fuser.Create<string, Json, FSharpList<Tuple<string, Json>>>(0, 2,
                        (key, value) => ListModule.Singleton(new Tuple<string, Json>(key, value)));
                default: return null; // This line should never be reached.
            }
        }

        public static readonly RuntimeFarkle<Json> Runtime =
            RuntimeFarkle<Json>.CreateFromBase64String(Definitions.Grammar.AsBase64,
                Farkle.CSharp.PostProcessor.Create<Json>(Transform, GetFuser));
    }
}
