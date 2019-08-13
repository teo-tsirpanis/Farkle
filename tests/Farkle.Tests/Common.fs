// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Expecto
open Farkle
open Farkle.Grammar
open Farkle.PostProcessor
open System.IO
open System.Reflection

// It guarantees to work regardless of current directory.
// The resources folder is copied to the output directory.
let resourcesPath = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName

let getResourceFile fileName = Path.Combine(resourcesPath, fileName)

let loadGrammar egtFile =
    getResourceFile egtFile
    |> GOLDParser.EGT.ofFile

let loadRuntimeFarkle egtFile =
    getResourceFile egtFile
    |> RuntimeFarkle.ofEGTFile PostProcessor.ast

let returnOrFail fmt x =
    match x with
    | Ok x -> x
    | Error x -> failtestf fmt <| box x