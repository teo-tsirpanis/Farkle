// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

extern alias farkle6;
using System;
using System.IO;
using Farkle6 = farkle6::Farkle;

namespace Farkle.Performance.Profiling;

internal static class Program
{
    private const int IterationCount = 1000;
    private const string JsonPath = "../../tests/resources/big.json";
    private const string Farkle6GrammarPath = "../../tests/resources/JSON.egtn";
    private const string Farkle7GrammarPath = "../../tests/resources/JSON.grammar.dat";
    private static string _jsonData;
    private static readonly Farkle6.RuntimeFarkle<object> _syntaxCheck =
        Farkle6.RuntimeFarkle<object>.Create(Farkle6.Grammar.EGT.ReadFromFile(Farkle6GrammarPath), Farkle6.PostProcessors.SyntaxChecker);
    private static readonly CharParser<object> _syntaxCheck7 =
        CharParser.CreateSyntaxChecker(Grammars.Grammar.Create(File.ReadAllBytes(Farkle7GrammarPath)));

    private static void Execute(Func<bool> f, string description)
    {
        Console.WriteLine($"Running {description}...");
        // GC.Collect(2, GCCollectionMode.Forced, true, true);
        for (var i = 0; i < IterationCount; i++)
            f();
    }

    private static bool ParseFarkle6() => _syntaxCheck.Parse(_jsonData).IsOk;

    private static bool ParseFarkle7() => _syntaxCheck7.Parse(_jsonData).IsSuccess;

    private static void Prepare()
    {
        _jsonData = File.ReadAllText(JsonPath);
        Console.WriteLine("Warming the JIT up...");
        for (int i = 0; i < 30; i++)
            if (!(ParseFarkle6() && ParseFarkle7()))
            {
                throw new Exception("Preparing went wrong.");
            }
    }

    internal static void Main()
    {
        Console.WriteLine("This program was made to help profiling Farkle.");
        Prepare();
        Execute(ParseFarkle6, "Farkle 6");
        Execute(ParseFarkle7, "Farkle 7");
    }
}
