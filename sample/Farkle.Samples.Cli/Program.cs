// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using System.IO;

namespace Farkle.Samples.Cli
{
    static class Program
    {
        static void Execute(Action f, string description)
        {
            Console.WriteLine($"Running {description}...");
            // GC.Collect(2, GCCollectionMode.Forced, true, true);
            for (var i = 0; i < 100; i++)
                f();
        }

        private const string JsonPath = "../../tests/resources/generated.json";
        private static string _jsonData;

        static void Main()
        {
            _jsonData = File.ReadAllText(JsonPath);
            Console.WriteLine("This program was made to help profiling Farkle.");
            
            Console.WriteLine("JITting the parser...");
            if (JSON.CSharp.Language.Runtime.Parse(_jsonData).IsError
                || JSON.FSharp.Language.runtime.Parse(_jsonData).IsError)
            {
                throw new Exception("Parsing went wrong...");
            }
            Execute(() => JSON.CSharp.Language.Runtime.Parse(_jsonData), "Farkle C#");
            Execute(() => JSON.FSharp.Language.runtime.Parse(_jsonData), "Farkle F#");
            Execute(() => FParsec.CharParsers.runParserOnString(Chiron.Parsing.jsonR.Value, null, "generated.json", _jsonData), "Chiron");
        }
    }
}
