// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Common
open Farkle.IO
open Farkle.JSON
open Farkle.Parser
open FsLexYacc.JSON
open FSharp.Text.Lexing
open System.IO

type TokenizerBenchmark() =

    let jsonFile = "generated.json"

    let farkleTokenize (rf: RuntimeFarkle<_>) =
        use f = File.OpenText jsonFile
        use cs = CharStream.Create f
        let grammar, oops = rf.Grammar |> returnOrFail
        let fTokenize() = Tokenizer.tokenize grammar.Groups grammar.DFAStates oops rf.PostProcessor ignore cs

        while fTokenize().IsSome do ()

    [<Benchmark(OperationsPerInvoke = 64)>]
    member _.FarkleCSharp() =
        for _ = 1 to 64 do
            farkleTokenize CSharp.Language.Runtime

    [<Benchmark(OperationsPerInvoke = 64)>]
    member _.FarkleFSharp() =
        for _ = 1 to 64 do
            farkleTokenize FSharp.Language.runtime

    [<Benchmark(Baseline = true)>]
    member _.FsLexYacc() =
        use f = File.OpenText jsonFile
        let lexBuf = LexBuffer<_>.FromTextReader f

        while Lexer.read lexBuf <> Parser.EOF do ()
