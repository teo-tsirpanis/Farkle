// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

extern alias farkle6;
using System;
using System.IO;
using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;
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
        CharParser.CreateSyntaxChecker(Grammar.CreateFromFile(Farkle7GrammarPath));
    private static readonly Farkle6.Parser.Tokenizer _tokenizer =
        new Farkle6.Parser.DefaultTokenizer(_syntaxCheck.GetGrammar());
    private static readonly Farkle.Parser.Tokenizers.Tokenizer<char> _tokenizer7 =
        Farkle.Parser.Tokenizers.Tokenizer.Create<char>(_syntaxCheck7.GetGrammar());

    private static void Execute(Func<bool> f, string description)
    {
        Console.WriteLine($"Running {description}...");
        // GC.Collect(2, GCCollectionMode.Forced, true, true);
        for (var i = 0; i < IterationCount; i++)
            f();
    }

    private static bool ParseFarkle6() => _syntaxCheck.Parse(_jsonData).IsOk;

    private static bool ParseFarkle7() => _syntaxCheck7.Parse(_jsonData).IsSuccess;

    private static bool TokenizeFarkle6()
    {
        var cs = new Farkle6.IO.CharStream(_jsonData);
        while (!_tokenizer.GetNextToken(Farkle6.PostProcessors.SyntaxChecker, cs).IsEOF)
        {
        }

        return true;
    }

    private static bool TokenizeFarkle7()
    {
        ParserState state = new();
        var reader = new ParserInputReader<char>(ref state, _jsonData);
        while (_tokenizer7.TryGetNextToken(ref reader, DummySemanticProvider<char>.Instance, out var token))
        {
            if (!token.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private static void Prepare()
    {
        _jsonData = File.ReadAllText(JsonPath);
        Console.WriteLine("Warming the JIT up...");
        for (int i = 0; i < 30; i++)
            if (!(ParseFarkle6() && ParseFarkle7() && TokenizeFarkle6() && TokenizeFarkle7()))
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
        Execute(TokenizeFarkle6, "Farkle 6 tokenizer");
        Execute(TokenizeFarkle7, "Farkle 7 tokenizer");
    }

    private sealed class DummySemanticProvider<TChar> : ITokenSemanticProvider<TChar>
    {
        public static readonly DummySemanticProvider<TChar> Instance = new();

        public object Transform(ref ParserState state, TokenSymbolHandle tokenSymbol, ReadOnlySpan<TChar> input) => null;
    }
}
