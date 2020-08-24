// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.IO
open Farkle.JSON
open Farkle.Parser
open FsLexYacc.JSON
open FSharp.Text.Lexing
open System.IO

type TokenizerBenchmark() =

    let jsonData = File.ReadAllText "generated.json"

    let farkleTokenize (rf: RuntimeFarkle<_>) =
        use f = new StringReader(jsonData)
        use cs = new CharStream(f)
        let grammar = rf.GetGrammar()
        let pp = rf.PostProcessor
        let tokenizer = DefaultTokenizer(grammar)

        while tokenizer.GetNextToken(pp, ignore, cs).IsSome do ()

    [<Benchmark>]
    member _.FarkleCSharp() =
        farkleTokenize CSharp.Language.Runtime

    [<Benchmark>]
    member _.FarkleFSharp() =
        farkleTokenize FSharp.Language.runtime

    [<Benchmark(Baseline = true)>]
    member _.FsLexYacc() =
        use f = new StringReader(jsonData)
        let lexBuf = LexBuffer<_>.FromTextReader f

        while Lexer.read lexBuf <> Parser.EOF do ()
