// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Benchmarks

open Chessie.ErrorHandling
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Attributes.Jobs
open Farkle
open Farkle.Grammar
open Farkle.Parser
open System
open System.Diagnostics
open System.IO
open System.Text

type InceptionBenchmark() =

    // This benchmark parses the official GOLD Meta-Language grammar with itself.
    [<Benchmark>]
    member x.InceptionBenchmarkFarkle() =
        let parser = GOLDParser("inception.egt", false)
        let (result, log) =
            parser.ParseFile "inception.grm"
            |> GOLDParser.FormatErrors
        result |> ofChoice |> returnOrFail

    [<Benchmark(Baseline = true)>]
    member x.InceptionBenchmarkLazarus() =
        let args = ProcessStartInfo()
        args.CreateNoWindow <- false
        args.FileName <- "goldtrcc.exe"
        args.Arguments <- "inception.egt inception.grm out.txt"
        args.RedirectStandardOutput <- false
        let proc = Process.Start args
        proc.WaitForExit()