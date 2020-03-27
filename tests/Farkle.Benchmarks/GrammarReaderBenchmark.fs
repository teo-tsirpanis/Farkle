// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open System
open System.IO
open System.Runtime.Serialization.Formatters.Binary

type GrammarReaderBenchmark() =

    let mutable base64EGT = ""

    let mutable base64EGTneo = ""

    [<GlobalSetup>]
    member __.Setup() =
        let bytes = File.ReadAllBytes "gml.egt"
        base64EGT <- Convert.ToBase64String bytes

        let grammar = EGT.ofBase64String base64EGT
        base64EGTneo <- EGT.toBase64StringNeo Base64FormattingOptions.None grammar

    [<Benchmark>]
    member __.Base64EGT() = EGT.ofBase64String base64EGT

    [<Benchmark(Baseline = true)>]
    member _.Base64EGTneo() = EGT.ofBase64String base64EGTneo

    [<Benchmark>]
    member __.BuildGrammar() =
        GOLDMetaLanguage.designtime
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> returnOrFail
