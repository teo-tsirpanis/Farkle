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

    let base64EGT =
        File.ReadAllBytes "gml.egt"
        |> Convert.ToBase64String

    let base64EGTneo =
        use stream = new MemoryStream()
        EGT.ofFile "gml.egt" |> EGT.toStreamNeo stream
        stream.ToArray()
        |> Convert.ToBase64String

    let designtime = GOLDMetaLanguage.designtime

    [<Benchmark>]
    member _.Base64EGT() = EGT.ofBase64String base64EGT

    [<Benchmark>]
    member _.Base64EGTneo() = EGT.ofBase64String base64EGTneo

    [<Benchmark>]
    member _.Build() =
        designtime
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> returnOrFail

    [<Benchmark>]
    member _.BuildPrecompiled() =
        RuntimeFarkle.buildUntyped GOLDMetaLanguage.designtime
