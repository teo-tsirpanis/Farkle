// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open System
open System.IO

type GrammarReaderBenchmark() =

    let mutable base64EGT = ""

    [<GlobalSetup>]
    member __.Setup() =
        let bytes = File.ReadAllBytes "gml.egt"
        base64EGT <- Convert.ToBase64String bytes

    [<Benchmark>]
    member __.Base64EGT() = EGT.ofBase64String base64EGT

    [<Benchmark>]
    member __.Build() =
        GOLDMetaLanguage.designtime
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> returnOrFail

    [<Benchmark>]
    member __.BuildPrecompiled() =
        RuntimeFarkle.buildUntyped GOLDMetaLanguage.designtime
