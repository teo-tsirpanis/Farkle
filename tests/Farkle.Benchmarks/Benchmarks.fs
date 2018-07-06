// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle.Parser
open System.Diagnostics

type InceptionBenchmark() =

    // This benchmark parses the official GOLD Meta-Language grammar with itself.
    [<Benchmark>]
    member x.InceptionBenchmarkFarkle() = GOLDParser("inception.egt").ParseFile("inception.grm", GOLDParserConfig.Default).ReductionOrFail()

    [<Benchmark(Baseline = true)>]
    member x.InceptionBenchmarkLazarus() =
        let args = ProcessStartInfo()
        args.CreateNoWindow <- false
        args.FileName <- "goldtrcc.exe"
        args.Arguments <- "inception.egt inception.grm out.txt"
        args.RedirectStandardOutput <- false
        let proc = Process.Start args
        proc.WaitForExit()