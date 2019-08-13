// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Common
open Farkle.IO
open System.IO
open Farkle.PostProcessor

/// This benchmark measures the performance of Farkle
/// (in both static and dynamic block mode, the latter with varying buffer sizes)
/// The task is to parse the GOLD Meta Language file describing the GOLD Meta Language.
type ParserBenchmark() =

    let rf = RuntimeFarkle.ofEGTFile PostProcessor.ast "gml.egt"

    let gmlContents = File.ReadAllText "gml.grm"

    [<Params (true, false)>]
    member val public DynamicallyReadInput = true with get, set

    member x.doIt pp =
        use cs =
            if x.DynamicallyReadInput then
                let sr = new StreamReader("gml.grm")
                CharStream.ofTextReader sr
            else
                CharStream.ofString gmlContents
        let rf = RuntimeFarkle.changePostProcessor pp rf
        RuntimeFarkle.parseChars rf ignore cs
        |> returnOrFail

    [<Benchmark>]
    member x.GOLDMetaLanguageAST() = x.doIt PostProcessor.ast

    [<Benchmark>]
    member x.GOLDMetaLanguageSyntaxCheck() = x.doIt PostProcessor.syntaxCheck
