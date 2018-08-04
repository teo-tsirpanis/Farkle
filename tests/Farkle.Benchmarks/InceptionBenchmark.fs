// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle.Parser
open System.Diagnostics
open System.Runtime.InteropServices

[<MemoryDiagnoser>]
/// This benchmark measures the performance of Farkle (in both lazy and eager mode),
/// and a native Pascal GOLD Parser engine I had written in the past.
/// Their task is to both read an EGT file describing the GOLD Meta Language, and then parse its source file.
type InceptionBenchmark() =
    let isWindows64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture = Architecture.X64

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleEager() = GOLDParser("inception.egt").ParseFile("inception.grm", GOLDParserConfig.Default.WithLazyLoad(false)).ResultOrFail()

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleLazy() = GOLDParser("inception.egt").ParseFile("inception.grm", GOLDParserConfig.Default.WithLazyLoad(true)).ResultOrFail()

    [<Benchmark>]
    member __.InceptionBenchmarkLazarus() =
        let args = ProcessStartInfo()
        args.CreateNoWindow <- false
        args.FileName <- "goldtrcc.exe"
        args.Arguments <- "inception.egt inception.grm out.txt"
        args.RedirectStandardOutput <- false
        if isWindows64 then
            let proc = Process.Start args
            proc.WaitForExit()