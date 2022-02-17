// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Farkle
open Farkle.Collections
open Farkle.Grammars
open System.Collections.Immutable
open System.IO
open System.Reflection

/// A very simple function to check if a string is recognized by a DFA.
/// We don't need a full-blown tokenizer here.
let matchDFAToString (states: ImmutableArray<DFAState>) str =
    let rec impl currState idx =
        if idx = String.length str then
            currState.AcceptSymbol
        else
            let newState =
                match currState.Edges.TryFind str.[idx] with
                | ValueSome s -> s
                | ValueNone -> currState.AnythingElse
            match newState with
            | Some s -> impl states.[int s] (idx + 1)
            | None -> None
    impl states.[0] 0

// It guarantees to work regardless of current directory.
// The resources folder is copied alongside with the executable.
let resourcesPath = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName

let allEGTFiles =
    Directory.GetFiles(resourcesPath, "*.egt")
    |> List.ofArray

let getResourceFile fileName = Path.Combine(resourcesPath, fileName)

let loadGrammar (egtFile: string) =
    getResourceFile egtFile
    |> EGT.ofFile

let loadRuntimeFarkle egtFile =
    loadGrammar egtFile
    |> RuntimeFarkle.create PostProcessors.ast
