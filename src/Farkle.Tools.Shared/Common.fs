// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tools.Common

open Serilog
open System
open System.IO
open System.Reflection
open System.Text

/// The version of the currently executing assembly.
let toolsVersion =
    let asm = Assembly.GetExecutingAssembly()
    asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion

let private equalsCI (x1: ReadOnlySpan<_>) (x2: string) =
    x1.Equals(x2.AsSpan(), StringComparison.OrdinalIgnoreCase)

let isProjectExtension x =
    equalsCI x ".csproj"
    || equalsCI x ".fsproj"
    || equalsCI x ".vbproj"
    || equalsCI x ".proj"

let isAssemblyExtension x =
    equalsCI x ".dll"
    || equalsCI x ".exe"

let isGrammarExtension x =
    // We even include CGT files so that they are fed to the
    // EGT reader which wil fail with a more specific message.
    equalsCI x ".cgt"
    || equalsCI x ".egt"
    || equalsCI x ".egtn"

let isElementUnique fBasedOn xs =
    let dict =
        xs
        |> Seq.groupBy fBasedOn
        |> Seq.collect (fun (_, xs) ->
            match Array.ofSeq xs with
            | [| |] -> Seq.empty
            | [|x|]-> Seq.singleton (x, true)
            | xs -> xs |> Seq.map (fun x -> x, false))
        |> readOnlyDict
    fun x -> dict.[x]

let private invalidFileNameChars = Path.GetInvalidFileNameChars()

let sanitizeUnsafeFileName (log: ILogger) (path: string) =
    match path.IndexOfAny(invalidFileNameChars) with
    | -1 -> path
    | _ ->
        log.Warning("{FileName} contains chatacters that cannot appear in a file name. They will be removed from the output file name.", path)
        let sb = StringBuilder(path.Length)
        for c in path do
            if not (Array.contains c invalidFileNameChars) then
                sb.Append(c) |> ignore
        sb.ToString()
