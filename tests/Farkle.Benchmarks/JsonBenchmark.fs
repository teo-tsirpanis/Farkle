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
open Farkle.PostProcessor
open FParsec
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Text

type JsonBenchmark() =

    let jsonFile = "generated.json"

    let syntaxChecker = RuntimeFarkle.changePostProcessor PostProcessor.syntaxCheck FSharp.Language.runtime

    [<Benchmark>]
    // There are performance differences between the F# and C# editions.
    // The separate benchmarks will stay for now.
    member __.FarkleCSharp() =
        RuntimeFarkle.parseFile CSharp.Language.Runtime ignore jsonFile
        |> returnOrFail

    [<Benchmark>]
    member __.FarkleFSharp() =
        RuntimeFarkle.parseFile FSharp.Language.runtime ignore jsonFile
        |> returnOrFail

    [<Benchmark>]  
    member __.FarkleSyntaxCheck() =
        RuntimeFarkle.parseFile syntaxChecker ignore jsonFile
        |> returnOrFail

    [<Benchmark>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member __.Chiron() =
        let parseResult = runParserOnFile !jsonR () jsonFile Encoding.UTF8
        match parseResult with
        | Success (json, _, _) -> json
        | Failure _ -> failwithf "Error while parsing '%s'" jsonFile

    [<Benchmark>]
    // Holy fuzzy, that was unbelievably fast! 🚄
    member __.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseFile jsonFile

    [<Benchmark(Baseline = true)>]
    // I was reluctant to add this, but I did, after I saw FsLexYacc in action.
    // I am interested to know how fast it can be. And yes, I know
    // about System.Text.Json! 😛
    member __.JsonNET() =
        use f = System.IO.File.OpenText jsonFile
        use jtr = new JsonTextReader(f)
        JToken.ReadFrom jtr
