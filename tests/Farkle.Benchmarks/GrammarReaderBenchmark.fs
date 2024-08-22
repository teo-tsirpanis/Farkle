// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle.Builder
open Farkle.Grammars
open Farkle.Samples.FSharp
open System
open System.IO
open System.Runtime.InteropServices

type GrammarReaderBenchmark() =

    let mutable egtNeo = Array.empty<byte>

    let mutable designtime = null

    let mutable readEGTNeo = Unchecked.defaultof<Func<_,_>>

    let mutable buildFarkle6 = Unchecked.defaultof<Func<_,_>>

    let mutable farkle7Grammar = Unchecked.defaultof<_>

    let mutable farkle7GrammarBuilder = Unchecked.defaultof<_>

    [<GlobalSetup>]
    member _.GlobalSetup() =
        let typ = Type.GetType("EntryPoints, Farkle6.Samples")
        egtNeo <- typ.GetMethod("convertToEGTNeo").Invoke(null, [| "gml.egt" |]) |> unbox
        designtime <- Type.GetType("Farkle.Samples.FSharp.GOLDMetaLanguage, Farkle6.Samples", true).GetProperty("designtime").GetValue(null, null)
        readEGTNeo <- typ.GetMethod(nameof readEGTNeo).CreateDelegate()
        buildFarkle6 <- typ.GetMethod("build").CreateDelegate()

        farkle7Grammar <- File.ReadAllBytes "gml.grammar.dat" |> ImmutableCollectionsMarshal.AsImmutableArray
        farkle7GrammarBuilder <- GOLDMetaLanguage.builder

    [<Benchmark>]
    member _.LoadEGTneoFarkle6() = readEGTNeo.Invoke egtNeo

    [<Benchmark>]
    member _.BuildFarkle6() =
        buildFarkle6.Invoke designtime

    [<Benchmark>]
    member _.LoadGrammarFarkle7() =
        Grammar.ofBytes farkle7Grammar

    [<Benchmark>]
    member _.BuildFarkle7() =
        GrammarBuilder.buildSyntaxCheck farkle7GrammarBuilder
