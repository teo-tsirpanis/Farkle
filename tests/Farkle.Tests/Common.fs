// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Farkle
open Farkle.Grammar
open System.IO
open System.Reflection

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
