// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open Farkle.Collections
open System.IO
open System.Text
open Farkle.PostProcessor

[<MemoryDiagnoser>]
/// This benchmark measures the performance of Farkle
/// (in both static and dynamic block mode, the latter with varying buffer sizes)
/// The task is to parse the GOLD Meta Language file describing the GOLD Meta Language.
type ParserBenchmark() =

    let rf = RuntimeFarkle.ofEGTFile PostProcessor.ast "gml.egt"

    let gmlContents = File.ReadAllText "gml.grm"

    [<Params (0, 256, 512, 1144, 2048)>]
    member val public BufferSize = 0 with get, set

    member x.doIt pp =
        use f = File.OpenRead "gml.grm"
        use sr = new StreamReader(f, Encoding.UTF8)
        use cs =
            if x.BufferSize = 0 then
                CharStream.ofString gmlContents
            else
                CharStream.ofTextReaderEx x.BufferSize sr
        let rf = RuntimeFarkle.changePostProcessor pp rf
        RuntimeFarkle.parseChars rf ignore cs
        |> returnOrFail

    [<Benchmark>]
    member x.GOLDMetaLanguageAST() = x.doIt PostProcessor.ast

    [<Benchmark>]
    member x.GOLDMetaLanguageSyntaxCheck() = x.doIt PostProcessor.syntaxCheck
