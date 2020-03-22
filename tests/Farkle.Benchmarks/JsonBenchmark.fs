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
open FParsec
open System.IO

type JsonBenchmark() =

    // File I/O during parsing will affect them, but the benchmarks measure
    // parsing when not the entire file is available on memory.
    let jsonFile = "generated.json"

    let jsonData = File.ReadAllText jsonFile

    let createTR() = new StringReader(jsonData) :> TextReader

    let syntaxChecker = RuntimeFarkle.changePostProcessor PostProcessors.syntaxCheck FSharp.Language.runtime

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

    [<Benchmark>]
    // The pseudo-static block mode (a dynamic block with
    // a preloaded buffer as large as the input) is quite
    // the waste on large inputs. That's why we parse from
    // string readers earlier.
    member __.FarkleFSharpStaticBlock() =
        RuntimeFarkle.parse FSharp.Language.runtime jsonData
        |> returnOrFail

    [<Benchmark>]
    member __.FarkleSyntaxCheck() =
        RuntimeFarkle.parseTextReader syntaxChecker ignore <| createTR()
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member __.Chiron() =
        let parseResult = run !jsonR jsonData
        match parseResult with
        | Success (json, _, _) -> json
        | Failure _ -> failwithf "Error while parsing '%s'" jsonFile

    #if BENCHMARK_3RD_PARTY
    [<Benchmark>]
    // Holy fuzzy, that was unbelievably fast! 🚄
    member __.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseTextReader <| createTR()

    [<Benchmark>]
    // I was reluctant to add this, but I did, after I saw FsLexYacc in action.
    // I am interested to know how fast it can be. And yes, I know
    // about System.Text.Json! 😛
    member __.JsonNET() =
        use jtr = new Newtonsoft.Json.JsonTextReader(createTR())
        Newtonsoft.Json.Linq.JToken.ReadFrom jtr
    #endif
