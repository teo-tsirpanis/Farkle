// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#nullable disable

using BenchmarkDotNet.Attributes;
using Farkle.Builder;

namespace Farkle.Benchmarks;

public class GroupBenchmark
{
    [Params(10, 10_000)] public int InputSize { get; set; }

    [ParamsAllValues] public bool EmitGroupOptimizedDfa { get; set; }

    private string _inputText;

    private CharParser<object> _parser;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var grammarBuilder = Group.Block("Group", "{", "}").AutoWhitespace(false);
        var options = new BuilderOptions() { EmitGroupOptimizedDfa = EmitGroupOptimizedDfa };
        _parser = grammarBuilder.BuildSyntaxCheck(options);
        _inputText = string.Create<object>(InputSize, null, (chars, _) =>
        {
            chars.Fill(' ');
            chars[0] = '{';
            chars[^1] = '}';
        });
    }

    [Benchmark]
    public ParserResult<object> Parse() => _parser.Parse(_inputText);
}
