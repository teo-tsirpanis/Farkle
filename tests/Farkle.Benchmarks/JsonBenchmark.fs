// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Chiron
open Farkle
open Farkle.Common
open Farkle.JSON
open System.IO

[<RankColumn>]
type JsonBenchmark() =

    // File I/O during parsing will affect them, but the benchmarks measure
    // parsing when not the entire file is immediately available on memory.
    let mutable jsonData = ""

    let createTR() = new StringReader(jsonData)

    let farkleRuntime = FSharp.Language.runtime

    [<Params("small.json", "medium.json", "big.json")>]
    member val FileName = "" with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        jsonData <- File.ReadAllText(this.FileName)

    [<Benchmark>]
    member _.Farkle() =
        RuntimeFarkle.parseTextReader farkleRuntime (createTR())
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member _.Chiron() = Json.parse jsonData

    [<Benchmark>]
    member _.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseTextReader (createTR())
