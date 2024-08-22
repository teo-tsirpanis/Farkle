// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Contains simple entry points to Farkle 6's functionality.
// To be called from benchmarks via reflection, to avoid directly
// referencing Farkle 6 and confuse F#.
module EntryPoints

open Farkle.Builder
open Farkle.Grammar
open Farkle.Samples.FSharp
open System.IO

let convertToEGTNeo path =
    use stream = new MemoryStream()
    EGT.ofFile path |> EGT.toStreamNeo stream
    stream.ToArray()

let readEGTNeo bytes =
    use stream = new MemoryStream(bytes, false)
    EGT.ofStream stream

let designtime = GOLDMetaLanguage.designtime

let returnOrFail = function | Ok x -> x | Error e -> failwith <| e.ToString()

let build (designtime: obj) =
    designtime
    |> unbox
    |> DesigntimeFarkleBuild.createGrammarDefinition
    |> DesigntimeFarkleBuild.buildGrammarOnly
    |> returnOrFail
