// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using System.IO;
using Farkle.CSharp;

namespace Farkle.JSON.Sample
{
    static class Program
    {
        static void Execute(Action f, string description)
        {
            Console.WriteLine($"Running {description}...");
            // GC.Collect(2, GCCollectionMode.Forced, true, true);
            for (var i = 0; i < 2; i++)
                f();
        }

        private static readonly string _jsonData = File.ReadAllText("../../tests/resources/generated.json");

        static void Main()
        {
            Console.WriteLine("This program was made to help profiling Farkle.");
            Execute(() => CSharp.Language.Runtime.Parse(_jsonData), "Farkle C#");
            Execute(() => FSharp.Language.runtime.Parse(_jsonData), "Farkle F#");
            Execute(() => FParsec.CharParsers.runParserOnString(Chiron.Parsing.jsonR.Value, null, "generated.json", _jsonData), "Chiron");
        }
    }
}
