// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Farkle.Grammars;
using Farkle.Parser.Semantics;
using ParserState = Farkle.Parser.ParserState;

namespace Farkle.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class JsonBenchmark
{
    [Params("small.json", "medium.json", "big.json")] public string FileName { get; set; }

    private byte[] _jsonBytes;

    private string _jsonText;

    private CharParser<object> _farkleParser;

    private Parser.Tokenizers.Tokenizer<char> _farkleTokenizer;

    private StreamReader JsonStreamReader() => new(new MemoryStream(_jsonBytes, false));

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonBytes = File.ReadAllBytes($"resources/{FileName}");
        _jsonText = File.ReadAllText($"resources/{FileName}");
        _farkleParser = CharParser.CreateSyntaxChecker(Grammar.Load("resources/JSON.grammar.dat"));
        _farkleTokenizer = Parser.Tokenizers.Tokenizer.Create<char>(_farkleParser.GetGrammar());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MemoryInput")]
    public object Farkle7String() => _farkleParser.Parse(_jsonText).Value;

    [Benchmark, BenchmarkCategory("MemoryInput")]
    public object PidginString() => PidginJsonParser.Parse(_jsonText).Value;

    [Benchmark, BenchmarkCategory("MemoryInput")]
    public object IronyString() => IronyJsonGrammar.Parse(_jsonText);

    // Testing these two libraries in parsing both strings and
    // streams is not important; both are suboptimally implemented
    // in one mode or another: FParsec copies the entire stream in
    // memory and FsYacc first copies the string to a byte array.

    [Benchmark, BenchmarkCategory("MemoryInput")]
    // FParsec's more optimized "Big Data edition" only supports .NET Framework.
    public void FParsecString() => FParsec.Json.ParseString(_jsonText, FileName);

    [Benchmark, BenchmarkCategory("MemoryInput")]
    public void FsLexYaccString() => FsLexYacc.Json.ParseString(_jsonText);

    [Benchmark(Baseline = true), BenchmarkCategory("StreamingInput")]
    public object FarkleStream() => _farkleParser.Parse(JsonStreamReader()).Value;

    [Benchmark, BenchmarkCategory("StreamingInput")]
    public object PidginStream() => PidginJsonParser.Parse(JsonStreamReader()).Value;

    [Benchmark(Baseline = true), BenchmarkCategory("Tokenize")]
    public bool FarkleTokenize()
    {
        ParserState state = new();
        var reader = new Parser.ParserInputReader<char>(ref state, _jsonText);
        while (_farkleTokenizer.TryGetNextToken(ref reader, DummySemanticProvider<char>.Instance, out var token))
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
