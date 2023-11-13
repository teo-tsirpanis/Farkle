// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

extern alias farkle6;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle6 = farkle6::Farkle;
using ParserState = Farkle.Parser.ParserState;

namespace Farkle.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class JsonBenchmark
{
    [Params("small.json", "medium.json", "big.json")] public string FileName { get; set; }

    private byte[] _jsonBytes;

    private string _jsonText;

    private Farkle6.RuntimeFarkle<object> _farkle6Runtime;

    private CharParser<object> _farkle7Parser;

    private Farkle6.Parser.Tokenizer _farkle6Tokenizer;

    private Parser.Tokenizers.Tokenizer<char> _farkle7Tokenizer;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonBytes = File.ReadAllBytes($"resources/{FileName}");
        _jsonText = File.ReadAllText($"resources/{FileName}");
        _farkle6Runtime = Farkle6.RuntimeFarkle<object>.Create(Farkle6.Grammar.EGT.ReadFromFile("resources/JSON.egt"), Farkle6.PostProcessors.SyntaxChecker);
        _farkle6Tokenizer = new Farkle6.Parser.DefaultTokenizer(_farkle6Runtime.GetGrammar());
        _farkle7Parser = CharParser.CreateSyntaxChecker(Grammar.Create(File.ReadAllBytes("resources/JSON.grammar.dat")));
        _farkle7Tokenizer = Parser.Tokenizers.Tokenizer.Create<char>(_farkle7Parser.GetGrammar());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MemoryInput")]
    public object Farkle6String() => _farkle6Runtime.Parse(_jsonText).ResultValue;

    [Benchmark, BenchmarkCategory("MemoryInput")]
    public object Farkle7String() => _farkle7Parser.Parse(_jsonText).Value;

    [Benchmark(Baseline = true), BenchmarkCategory("StreamingInput")]
    public object Farkle6Stream() => _farkle6Runtime.Parse(new StreamReader(new MemoryStream(_jsonBytes, false))).ResultValue;

    [Benchmark, BenchmarkCategory("StreamingInput")]
    public object Farkle7Stream() => _farkle7Parser.Parse(new StreamReader(new MemoryStream(_jsonBytes, false))).Value;

    [Benchmark(Baseline = true), BenchmarkCategory("Tokenize")]
    public object Farkle6Tokenize()
    {
        var cs = new Farkle6.IO.CharStream(_jsonText);
        while (!_farkle6Tokenizer.GetNextToken(Farkle6.PostProcessors.SyntaxChecker, cs).IsEOF)
        {
        }

        return true;
    }

    [Benchmark, BenchmarkCategory("Tokenize")]
    public object Farkle7Tokenize()
    {
        ParserState state = new();
        var reader = new Parser.ParserInputReader<char>(ref state, _jsonText);
        while (_farkle7Tokenizer.TryGetNextToken(ref reader, DummySemanticProvider<char>.Instance, out var token))
        {
            if (!token.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class DummySemanticProvider<TChar> : ITokenSemanticProvider<TChar>
    {
        public static readonly DummySemanticProvider<TChar> Instance = new();

        public object Transform(ref ParserState state, TokenSymbolHandle symbol, ReadOnlySpan<TChar> input) => null;
    }
}
