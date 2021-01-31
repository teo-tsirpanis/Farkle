// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using System.IO;
using static Farkle.Builder.DesigntimeFarkleBuild;

namespace Farkle.Samples.Cli
{
    internal static class Program
    {
        private const int IterationCount = 1000;
        private const string JsonPath = "../../tests/resources/big.json";
        private static string _jsonData;
        private static readonly RuntimeFarkle<object> _syntaxCheck = CSharp.JSON.Runtime.SyntaxCheck();

        private static void Execute(Func<bool> f, string description)
        {
            Console.WriteLine($"Running {description}...");
            // GC.Collect(2, GCCollectionMode.Forced, true, true);
            for (var i = 0; i < IterationCount; i++)
                f();
        }

        private static bool ParseFarkle<T>(this RuntimeFarkle<T> rf) => rf.Parse(_jsonData).IsOk;
        private static bool BuildJson() => Build(CSharp.JSON.Designtime).Item1.IsOk;

        private static bool BuildGoldMetaLanguage() =>
            BuildGrammarOnly(CreateGrammarDefinition(FSharp.GOLDMetaLanguage.designtime)).IsOk;

        private static bool ParseFarkleCSharp() => CSharp.JSON.Runtime.ParseFarkle();
        private static bool ParseFarkleFSharp() => FSharp.JSON.Runtime.ParseFarkle();
        private static bool ParseFarkleSyntaxCheck() => _syntaxCheck.ParseFarkle();
        private static bool ParseChiron() => FParsec.CharParsers.run(Chiron.Parsing.jsonR.Value, _jsonData).IsSuccess;

        private static void Prepare()
        {
            _jsonData = File.ReadAllText(JsonPath);
            Console.WriteLine("Warming the JIT up...");
            for (int i = 0; i < 30; i++)
                if (!(BuildJson() && BuildGoldMetaLanguage() && ParseFarkleCSharp() && ParseFarkleFSharp() &&
                      ParseFarkleSyntaxCheck() && ParseChiron()))
                {
                    throw new Exception("Preparing went wrong.");
                }
        }

        internal static void Main()
        {
            Console.WriteLine("This program was made to help profiling Farkle.");
            Prepare();
            Execute(ParseFarkleCSharp, "Farkle C#");
            Execute(ParseFarkleFSharp, "Farkle F#");
            Execute(ParseFarkleSyntaxCheck, "Farkle Syntax Check");
            Execute(BuildJson, "Farkle Build JSON");
            Execute(BuildGoldMetaLanguage, "Farkle Build GOLD Meta-Language");
            Execute(ParseChiron, "Chiron");
        }
    }
}
