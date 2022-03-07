// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Chiron
open Farkle
open Farkle.Samples
open FParsec
open System
open System.IO
open System.Text

[<RankColumn>]
type JsonBenchmark() =

    let mutable jsonBytes = Array.Empty()

    let mutable jsonText = ""

    let createTR() = new StreamReader(new MemoryStream(jsonBytes, false))

    let farkleRuntime = FSharp.JSON.runtime.Cast()

    let farkleRuntimeSyntaxCheck = FSharp.JSON.runtime.SyntaxCheck()

    [<Params("small.json", "medium.json", "big.json")>]
    member val FileName = "" with get, set

    // Benchmarking syntax-checking tests the raw speed of the
    // parsers, without the overhead of the allocated JSON objects.
    [<Params(true, false)>]
    member val SyntaxCheck = true with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        jsonText <- File.ReadAllText this.FileName
        jsonBytes <- Encoding.UTF8.GetBytes jsonText

    [<Benchmark>]
    // Testing the other two libraries in parsing both strings and
    // streams is not important; both are suboptimally implemented
    // in one mode or another: FParsec copies the entire stream in
    // memory and FsYacc first copies the string in a byte array.
    member this.FarkleStream() =
        let rf =
            if this.SyntaxCheck then
                farkleRuntimeSyntaxCheck
            else
                farkleRuntime
        use tr = createTR()
        RuntimeFarkle.parseTextReader rf tr

    [<Benchmark>]
    member this.FarkleString() =
        let rf =
            if this.SyntaxCheck then
                farkleRuntimeSyntaxCheck
            else
                farkleRuntime
        RuntimeFarkle.parseString rf jsonText

    [<Benchmark(Baseline = true)>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // Its more optimized "Big Data edition" only supports .NET Framework.
    member this.Chiron() =
        let parser =
            if this.SyntaxCheck then
                JsonSyntaxCheckers.Chiron.jsonParser
            else
                jsonR.Value
        runParserOnString parser () this.FileName jsonText

    [<Benchmark>]
    member this.FsLexYacc() =
        if this.SyntaxCheck then
            JsonSyntaxCheckers.FsLexYacc.parseString jsonText
        else
            FsLexYacc.JSON.JSONParser.parseString jsonText
