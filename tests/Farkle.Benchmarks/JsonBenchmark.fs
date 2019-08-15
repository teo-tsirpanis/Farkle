// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Chiron
open Farkle
open Farkle.Common
open Farkle.IO
open Farkle.JSON
open Farkle.PostProcessor
open FParsec
open System.IO
open System.Text

type JsonBenchmark() =

    let jsonFile = "generated.json"

    let syntaxChecker = RuntimeFarkle.changePostProcessor PostProcessor.syntaxCheck FSharp.Language.runtime

    [<Params(true, false)>]
    member val DynamicallyReadInput = true with get, set

    member x.GetCharStream() =
        if x.DynamicallyReadInput then
            let sr = File.OpenText(jsonFile)
            CharStream.ofTextReader sr
        else
            jsonFile |> File.ReadAllText |> CharStream.ofString

    [<Benchmark>]
    member x.FarkleCSharp() =
        use cs = x.GetCharStream()
        RuntimeFarkle.parseChars CSharp.Language.Runtime ignore cs
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    member x.FarkleFSharp() =
        use cs = x.GetCharStream()
        RuntimeFarkle.parseChars FSharp.Language.runtime ignore cs
        |> returnOrFail

    [<Benchmark>]  
    member x.FarkleSyntaxCheck() =
        use cs = x.GetCharStream()
        RuntimeFarkle.parseChars syntaxChecker ignore cs
        |> returnOrFail

    [<Benchmark>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member x.Chiron() =
        let parseResult =
            if x.DynamicallyReadInput then
                runParserOnFile !jsonR () jsonFile Encoding.UTF8
            else
                jsonFile |> File.ReadAllText |> runParserOnString !jsonR () jsonFile
        match parseResult with
        | Success (json, _, _) -> json
        | Failure _ -> failwithf "Error while parsing '%s'" jsonFile
