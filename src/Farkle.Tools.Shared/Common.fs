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

let private endsWithCI (x1: string) x2 =
    x1.EndsWith(x2, StringComparison.OrdinalIgnoreCase)

let isProjectPath x =
    endsWithCI x ".csproj"
    || endsWithCI x ".fsproj"
    || endsWithCI x ".vbproj"
    || endsWithCI x ".proj"

let isAssemblyPath x =
    endsWithCI x ".dll"
    || endsWithCI x ".exe"

let isGrammarPath x =
    endsWithCI x ".cgt"
    || endsWithCI x ".egt"
    || endsWithCI x ".egtn"
    || endsWithCI x ".grammar.dat"

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
