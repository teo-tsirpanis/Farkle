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
        let cs = CharStream.ofTextReader f
        let grammar, oops = rf.Grammar |> returnOrFail
        let fTokenize() = Tokenizer.tokenize grammar.Groups grammar.DFAStates oops rf.PostProcessor ignore cs

        let tokens = ResizeArray()
        let mutable tok = fTokenize()
        while tok.IsSome do
            tokens.Add tok.Value
            tok <- fTokenize()
        tokens

    [<Benchmark>]
    member _.FarkleCSharp() = farkleTokenize CSharp.Language.Runtime

    [<Benchmark>]
    member _.FarkleFSharp() = farkleTokenize FSharp.Language.runtime

    [<Benchmark(Baseline = true)>]
    member _.FsLexYacc() =
        use f = File.OpenText jsonFile
        let lexBuf = LexBuffer<_>.FromTextReader f

        let mutable tok = Lexer.read lexBuf
        let tokens = ResizeArray()
        while tok <> Parser.EOF do
            tokens.Add tok
            tok <- Lexer.read lexBuf
        tokens