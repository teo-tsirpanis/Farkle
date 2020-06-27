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
open System.Text.Json

[<RankColumn>]
type JsonBenchmark() =

    // File I/O during parsing will affect them, but the benchmarks measure
    // parsing when not the entire file is available on memory.
    let jsonFile = "generated.json"

    let jsonData = File.ReadAllText jsonFile

    let createTR() = new StringReader(jsonData) :> TextReader

    [<Benchmark>]
    // There are performance differences between the F# and C# editions.
    // The separate benchmarks will stay for now.
    member __.FarkleCSharp() =
        RuntimeFarkle.parseTextReader CSharp.Language.Runtime ignore <| createTR()
        |> returnOrFail

    [<Benchmark>]
    member __.FarkleFSharp() =
        RuntimeFarkle.parseTextReader FSharp.Language.runtime ignore <| createTR()
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member __.Chiron() = Json.parse jsonData

    [<Benchmark>]
    member __.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseTextReader <| createTR()

    [<Benchmark>]
    member __.JsonNET() =
        use jtr = new Newtonsoft.Json.JsonTextReader(createTR())
        Newtonsoft.Json.Linq.JToken.ReadFrom jtr

    [<Benchmark>]
    member _.SystemTextJson() = JsonDocument.Parse jsonData
