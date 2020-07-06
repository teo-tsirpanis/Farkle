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
    let jsonData = File.ReadAllText "generated.json"

    let createTR() = new StringReader(jsonData)

    [<Benchmark>]
    // There are performance differences between the F# and C# editions.
    // The separate benchmarks will stay for now.
    member _.FarkleCSharp() =
        RuntimeFarkle.parseTextReader CSharp.Language.Runtime ignore (createTR())
        |> returnOrFail

    [<Benchmark>]
    member _.FarkleFSharp() =
        RuntimeFarkle.parseTextReader FSharp.Language.runtime ignore (createTR())
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member _.Chiron() = Json.parse jsonData

    [<Benchmark>]
    member _.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseTextReader (createTR())

    // Sprache was about to be included but it's
    // significanty slower than the other libraries.

    [<Benchmark>]
    member _.JsonNET() =
        use jtr = new Newtonsoft.Json.JsonTextReader(createTR())
        Newtonsoft.Json.Linq.JToken.ReadFrom jtr

    [<Benchmark>]
    member _.SystemTextJson() = System.Text.Json.JsonDocument.Parse(jsonData).Dispose()
