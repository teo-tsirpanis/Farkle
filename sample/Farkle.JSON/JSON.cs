// This file was created by Farkle.Tools and is a skeleton
// to help you write a post-processor for JSON.
// You should complete it yourself, and keep it to source control.

using System;
using Farkle;
using Farkle.CSharp;
using JSON.Definitions;

namespace JSON {
    public static class Language {

        // This function converts terminals to anything you want.
        // If you do not care about a terminal (like single characters),
        // you can let the default case return null.
        private static object Transform(uint terminal, Position position, ReadOnlySpan<char> data) {
            switch ((Terminal) terminal) {
                case Terminal.Comma :
                    return data;
                case Terminal.Colon :
                    return data;
                case Terminal.LBracket :
                    return data;
                case Terminal.RBracket :
                    return data;
                case Terminal.LBrace :
                    return data;
                case Terminal.RBrace :
                    return data;
                case Terminal.False :
                    return data;
                case Terminal.Null :
                    return data;
                case Terminal.Number :
                    return data;
                case Terminal.String :
                    return data;
                case Terminal.True :
                    return data;
                default: return null;
            }
        }

        // The fusers merge the parts of a production into one object of your desire.
        // This function maps each production to a fuser.
        // Do not delete anything here, or the post-processor will fail.
        private static Fuser GetFuser(uint prod) {
            switch ((Production) prod) {
                case Production.ValueString:
                    return Fuser.Create();
                case Production.ValueNumber:
                    return Fuser.Create();
                case Production.Value2:
                    return Fuser.Create();
                case Production.Value2:
                    return Fuser.Create();
                case Production.ValueTrue:
                    return Fuser.Create();
                case Production.ValueFalse:
                    return Fuser.Create();
                case Production.ValueNull:
                    return Fuser.Create();
                case Production.ArrayLBracketRBracket:
                    return Fuser.Create();
                case Production.ArrayElementComma:
                    return Fuser.Create();
                case Production.ArrayElement:
                    return Fuser.Create();
                case Production.ObjectLBraceRBrace:
                    return Fuser.Create();
                case Production.ObjectElementStringColonComma:
                    return Fuser.Create();
                case Production.ObjectElement:
                    return Fuser.Create();
                default: return null; // This line should never be reached.
            }
        }

        public static readonly RuntimeFarkle<TODO> Runtime =
            RuntimeFarkle<TODO>.CreateFromBase64(Grammar.AsBase64,PostProcessor.Create<TODO>(Transform, GetFuser));
    }
}
