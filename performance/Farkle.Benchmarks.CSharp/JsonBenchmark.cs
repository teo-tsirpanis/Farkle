// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

extern alias farkle6;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Farkle.Grammars;
using Farkle6 = farkle6::Farkle;

namespace Farkle.Benchmarks.CSharp;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class JsonBenchmark
{
    [Params("small.json", "medium.json", "big.json")] public string FileName { get; set; }

    private byte[] _jsonBytes;

    private string _jsonText;

    private Farkle6.RuntimeFarkle<object> _farkle6Runtime;

    private CharParser<object> _farkle7Parser;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonBytes = File.ReadAllBytes($"resources/{FileName}");
        _jsonText = File.ReadAllText($"resources/{FileName}");
        _farkle6Runtime = Farkle6.RuntimeFarkle<object>.Create(Farkle6.Grammar.EGT.ReadFromFile("resources/JSON.egt"), Farkle6.PostProcessors.SyntaxChecker);
        _farkle7Parser = CharParser.CreateSyntaxChecker(Grammar.Create(File.ReadAllBytes("resources/JSON.grammar.dat")));
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MemoryInput")]
    public object Farkle6String() => _farkle6Runtime.Parse(_jsonText).ResultValue;

    [Benchmark, BenchmarkCategory("MemoryInput")]
    public object Farkle7String() => _farkle7Parser.Parse(_jsonText).Value;

    [Benchmark(Baseline = true), BenchmarkCategory("StreamingInput")]
    public object Farkle6Stream() => _farkle6Runtime.Parse(new StreamReader(new MemoryStream(_jsonBytes, false))).ResultValue;

    [Benchmark, BenchmarkCategory("StreamingInput")]
    public object Farkle7Stream() => _farkle7Parser.Parse(new StreamReader(new MemoryStream(_jsonBytes, false))).Value;
}
