// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

extern alias farkle6;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Farkle.Grammars;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Farkle6 = farkle6::Farkle;

namespace Farkle.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GrammarReaderBenchmark
{
    [Params("JSON", "COBOL85")] public string Grammars { get; set; }

    private byte[] Egt, EgtNeo;

    private ImmutableArray<byte> Farkle7Grammar;

    [GlobalSetup]
    public void GlobalSetup()
    {
        Egt = File.ReadAllBytes($"resources/{Grammars}.egt");
        EgtNeo = File.ReadAllBytes($"resources/{Grammars}.egtn");
        Farkle7Grammar = ImmutableCollectionsMarshal.AsImmutableArray(File.ReadAllBytes($"resources/{Grammars}.grammar.dat"));
    }

    [BenchmarkCategory("Read"), Benchmark(Baseline = true)]
    public object ReadFarkle6() =>
        Farkle6.Grammar.EGT.ReadFromStream(new MemoryStream(EgtNeo, false));

    [BenchmarkCategory("Read"), Benchmark]
    public object ReadFarkle7() =>
        Grammar.Create(Farkle7Grammar);

    [BenchmarkCategory("Read"), Benchmark]
    public object ReadFarkle7NoValidation() =>
        Grammar.CreateUnsafe(Farkle7Grammar);

    [BenchmarkCategory("Convert"), Benchmark(Baseline = true)]
    public object ConvertFarkle6() =>
        Farkle6.Grammar.EGT.ReadFromStream(new MemoryStream(Egt, false));

    [BenchmarkCategory("Convert"), Benchmark]
    public object ConvertFarkle7() =>
        Grammar.CreateFromGoldParserGrammar(new MemoryStream(Egt, false));
}
