// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;
using System.Runtime.CompilerServices;
using Farkle.Builder;
using Farkle.Samples.CSharp;

namespace Farkle.Performance.Profiling;

internal static class Program
{
    private const int IterationCount = 10_000;
    private const string JsonPath = "../../tests/resources/big.json";
    private const string FarkleGrammarPath = "../../tests/resources/JSON.grammar.dat";
    private static string _jsonData = File.ReadAllText(JsonPath);
    private static readonly CharParser<object?> _syntaxCheck =
        CharParser.CreateSyntaxChecker(Grammar.Load(FarkleGrammarPath));
    private static readonly Farkle.Parser.Tokenizers.Tokenizer<char> _tokenizer =
        Farkle.Parser.Tokenizers.Tokenizer.Create<char>(_syntaxCheck.GetGrammar());
    private static readonly IGrammarBuilder GrammarIelr = SimpleMaths.Builder;
    private static readonly IGrammarBuilder GrammarLalr = SimpleMaths.Builder.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1);

    private static void Execute(Func<bool> f, [CallerArgumentExpression(nameof(f))] string? description = null)
    {
        Console.WriteLine($"Running {description}...");
        // GC.Collect(2, GCCollectionMode.Forced, true, true);
        for (var i = 0; i < IterationCount; i++)
            f();
    }

    private static bool Parse() => _syntaxCheck.Parse(_jsonData).IsSuccess;

    private static bool Tokenize()
    {
        ParserState state = new();
        var reader = new ParserInputReader<char>(ref state, _jsonData);
        while (_tokenizer.TryGetNextToken(ref reader, DummySemanticProvider<char>.Instance, out var token))
        {
            if (!token.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private static bool BuildLalr() => !GrammarLalr.BuildSyntaxCheck(BuilderOutputs.GrammarLrStateMachine).Grammar!.LrStateMachine!.HasConflicts;
    
    private static bool BuildIelr() => !GrammarIelr.BuildSyntaxCheck(BuilderOutputs.GrammarLrStateMachine).Grammar!.LrStateMachine!.HasConflicts;

    private static void Prepare()
    {
        Console.WriteLine("Warming the JIT up...");
        Thread.Sleep(100); // Wait for the tiered compilation counting delay.
        for (int i = 0; i < 30; i++)
        {
            if (!(Parse() && Tokenize() && BuildLalr() && BuildIelr()))
            {
                throw new Exception("Preparing went wrong.");
            }
        }
    }

    internal static void Main()
    {
        Console.WriteLine("This program was made to help profiling Farkle.");
        Prepare();
        Execute(Parse);
        Execute(Tokenize);
        Execute(BuildLalr);
        Execute(BuildIelr);
    }

    private sealed class DummySemanticProvider<TChar> : ITokenSemanticProvider<TChar>
    {
        public static readonly DummySemanticProvider<TChar> Instance = new();

        public object? Transform(ref ParserState state, TokenSymbolHandle tokenSymbol, ReadOnlySpan<TChar> input) => null;
    }
}
