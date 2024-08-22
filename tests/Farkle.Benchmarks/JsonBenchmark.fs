// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Samples
open FParsec
open System
open System.IO
open System.Text

[<RankColumn; MemoryDiagnoser>]
type JsonBenchmark() =

    let mutable jsonBytes = Array.Empty()

    let mutable jsonText = ""

    static let farkleRuntimeSyntaxCheck = CharParser.syntaxCheck FSharp.JSON.parser

    [<Params("small.json", "medium.json", "big.json")>]
    member val FileName = "" with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        jsonText <- File.ReadAllText this.FileName
        jsonBytes <- Encoding.UTF8.GetBytes jsonText

    [<Benchmark>]
    // Testing the other two libraries in parsing both strings and
    // streams is not important; both are suboptimally implemented
    // in one mode or another: FParsec copies the entire stream in
    // memory and FsYacc first copies the string in a byte array.
    member _.FarkleStream() =
        use tr = new StreamReader(new MemoryStream(jsonBytes, false))
        CharParser.parseTextReader farkleRuntimeSyntaxCheck tr
        |> _.Value

    [<Benchmark>]
    member _.FarkleString() =
        CharParser.parseString farkleRuntimeSyntaxCheck jsonText
        |> _.Value

    [<Benchmark(Baseline = true)>]
    // FParsec's more optimized "Big Data edition" only supports .NET Framework.
    member this.FParsec() =
        runParserOnString FParsec.JSON.JSONParser.jsonParser () this.FileName jsonText
        |> function | Success((), _, _) -> () | Failure(_, error, _) -> failwithf "%O" error

    [<Benchmark>]
    member _.FsLexYacc() = FsLexYacc.JSON.JSONParser.parseString jsonText
