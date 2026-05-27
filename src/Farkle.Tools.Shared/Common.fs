// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tools.Common

open Serilog
open System
open System.Buffers
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
    equalsCI x ".cgt"
    || equalsCI x ".egt"
    || equalsCI x ".egtn"
    || equalsCI x ".grammar.dat"

let private invalidFileNameChars = SearchValues.Create(Path.GetInvalidFileNameChars())

let sanitizeUnsafeFileName (log: ILogger) (path: string) =
    match path.AsSpan().IndexOfAny(invalidFileNameChars) with
    | -1 -> path
    | _ ->
        log.Warning("{FileName} contains characters that cannot appear in a file name. They will be removed from the output file name.", path)
        let sb = StringBuilder(path.Length)
        for c in path do
            if not (invalidFileNameChars.Contains c) then
                sb.Append(c) |> ignore
        sb.ToString()
