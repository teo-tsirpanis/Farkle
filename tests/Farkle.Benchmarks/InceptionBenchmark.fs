// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Grammar.GOLDParser
open Farkle.Parser
open System.Diagnostics
open System.Text
open System.Runtime.InteropServices

[<MemoryDiagnoser>]
/// This benchmark measures the performance of Farkle (in both lazy and eager mode),
/// and a native Pascal GOLD Parser engine I had written in the past.
/// Their task is to both read an EGT file describing the GOLD Meta Language, and then parse its source file.
type InceptionBenchmark() =
    let isWindows64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture = Architecture.X64

    [<DllImport("goldparser_win64.dll", CallingConvention = CallingConvention.StdCall)>]
    static extern int ParseFile([<MarshalAs(UnmanagedType.LPStr)>] string EGTFile, [<MarshalAs(UnmanagedType.LPStr)>] string InputFile)

    member inline __.doIt lazyLoad =
        "inception.egt"
        |> EGT.ofFile
        |> Result.map (fun g -> GOLDParser.parseFile g ignore lazyLoad Encoding.UTF8 "inception.grm")
        |> returnOrFail

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleEager() = __.doIt false

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleLazy() = __.doIt true

    [<Benchmark(Baseline=true)>]
    member __.InceptionBenchmarkLazarus() =
        if isWindows64 then
            if ParseFile("inception.egt", "inception.grm") <> 0 then
                failwith "Native GOLD Parser failed"