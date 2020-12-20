// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle.Builder
open Farkle.Common.Result
open Farkle.Grammar
open Farkle.Samples.FSharp
open System.IO

type GrammarReaderBenchmark() =

    let egtNeo =
        use stream = new MemoryStream()
        EGT.ofFile "gml.egt" |> EGT.toStreamNeo stream
        stream.ToArray()

    let designtime = GOLDMetaLanguage.designtime

    [<Benchmark>]
    member _.EGTneo() =
        use stream = new MemoryStream(egtNeo, false)
        EGT.ofStream stream

    [<Benchmark>]
    member _.Build() =
        designtime
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> returnOrFail
