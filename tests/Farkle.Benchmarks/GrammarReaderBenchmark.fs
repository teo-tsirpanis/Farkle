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

type GrammarReaderBenchmark() =

    let mutable base64EGT = ""

    [<VolatileField>]
    let mutable builtGrammar = Unchecked.defaultof<_>

    [<GlobalSetup>]
    member __.Setup() =
        let bytes = File.ReadAllBytes "gml.egt"
        base64EGT <- Convert.ToBase64String bytes

    [<Benchmark>]
    member __.Base64EGT() =
        base64EGT |> EGT.ofBase64String

    [<Benchmark(OperationsPerInvoke = 100)>]
    member __.BuildGrammar() =
        for __ = 0 to 99 do
            builtGrammar <-
                GOLDMetaLanguage.designtime
                |> DesigntimeFarkleBuild.createGrammarDefinition
                |> DesigntimeFarkleBuild.buildGrammarOnly
                |> returnOrFail
