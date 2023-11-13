// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Farkle
open Farkle.Grammars
open Farkle.Parser.Semantics
open System.Collections.Immutable
open System
open System.IO
open System.Reflection

#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
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
#endif

// It guarantees to work regardless of current directory.
// The resources folder is copied alongside with the executable.
let resourcesPath = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName

let allEGTFiles =
    Directory.GetFiles(resourcesPath, "*.egt")
    |> List.ofArray

let getResourceFile fileName = Path.Combine(resourcesPath, fileName)

let loadGrammar (egtFile: string) =
    let resourceFile = getResourceFile egtFile
    match Path.GetExtension egtFile with
    | ".cgt" | ".egt" ->
        use stream = File.OpenRead resourceFile
        Grammar.ofGoldParserStream stream
    | _ ->
        Grammar.ofFile resourceFile

let loadCharParser egtFile =
    loadGrammar egtFile
    |> CharParser.createSyntaxCheck

let listOfSpan (span: ReadOnlySpan<_>) =
    let mutable list = []
    for i = span.Length - 1 downto 0 do
        list <- span.[i] :: list
    list
