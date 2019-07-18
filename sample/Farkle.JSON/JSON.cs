// This file was created by Farkle.Tools and is a skeleton
// to help you write a post-processor for JSON.
// You should complete it yourself, and keep it to source control.

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
                        decimal.Parse(data);
#else
                        decimal.Parse(data.ToString());
#endif
                    // Avoid boxing by wrapping directly to the Json type.
                    return Json.NewNumber(num);
                case Terminal.String:
                    return Json.NewString(UnescapeJsonString(data));
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
                    return Fuser.First;
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
                case Production.ArrayElementComma:
                    return Fuser.Create<Json, FSharpList<Json>, FSharpList<Json>>(0, 2, FSharpList<Json>.Cons);
                case Production.ArrayElementEmpty:
                    return Fuser.Constant(FSharpList<Json>.Empty);
                case Production.ObjectLBraceRBrace:
                    return Fuser.Create<FSharpMap<string, Json>, Json>(1, Json.NewObject);
                case Production.ObjectElementStringColonComma:
                    return Fuser.Create<string, Json, FSharpMap<string, Json>, FSharpMap<string, Json>>(0, 2, 4,
                        (key, value, map) => map.Add(key, value));
                case Production.ObjectElementEmpty:
                    return Fuser.Constant(MapModule.Empty<string, Json>());
                default: return null; // This line should never be reached.
            }
        }

        public static readonly RuntimeFarkle<Json> Runtime =
            RuntimeFarkle<Json>.CreateFromBase64String(Definitions.Grammar.AsBase64,
                Farkle.CSharp.PostProcessor.Create<Json>(Transform, GetFuser));
    }
}