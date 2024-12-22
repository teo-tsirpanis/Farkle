// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Farkle.Grammars;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Farkle.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GrammarReaderBenchmark
{
    [Params("JSON", "COBOL85")] public string Grammars { get; set; }

    private byte[] Egt;

    private ImmutableArray<byte> Farkle7Grammar;

    [GlobalSetup]
    public void GlobalSetup()
    {
        Egt = File.ReadAllBytes($"resources/{Grammars}.egt");
        Farkle7Grammar = ImmutableCollectionsMarshal.AsImmutableArray(File.ReadAllBytes($"resources/{Grammars}.grammar.dat"));
    }

    [BenchmarkCategory("Read"), Benchmark(Baseline = true)]
    public object ReadFarkle7() =>
        Grammar.Load(Farkle7Grammar);

    [BenchmarkCategory("Read"), Benchmark]
    public object ReadFarkle7NoValidation() =>
        Grammar.LoadUnsafe(Farkle7Grammar);

    [BenchmarkCategory("Convert"), Benchmark(Baseline = true)]
    public object ConvertFarkle7() =>
        Grammar.ConvertFromGoldParser(new MemoryStream(Egt, false));
}
