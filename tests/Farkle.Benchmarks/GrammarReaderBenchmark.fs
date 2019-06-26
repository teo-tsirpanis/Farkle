// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Loggers
open Farkle.Common
open Farkle.Grammar.GOLDParser
open System
open System.IO

type GrammarReaderBenchmark() =

    let logger = BenchmarkDotNet.Loggers.ConsoleLogger() :> ILogger

    let mutable base64EGT = ""

    [<GlobalSetup>]
    member __.Setup() =
        let bytes = File.ReadAllBytes "gml.egt"
        base64EGT <- Convert.ToBase64String bytes
        logger.WriteLineInfo <| sprintf "EGT as Base64: %d characters" base64EGT.Length

    [<Benchmark>]
    member __.Base64EGT() =
        base64EGT |> EGT.ofBase64String |> returnOrFail
