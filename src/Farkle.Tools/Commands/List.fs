// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.List

open Argu
open Farkle.Monads.Either
open Farkle.Tools
open Serilog
open System
open System.IO
open System.Text.Json

type Arguments =
    | [<ExactlyOnce; MainCommand>] InputFile of string
    | [<Unique>] Configuration of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | InputFile _ -> "The assembly or project file from which to look for precompiled grammars."
            | Configuration _ -> "The configuration the project will be evaluated with. Defaults to Debug."

let private getAssemblyFile projectOptions (file: string) = either {
    let ext = Path.GetExtension(file.AsSpan())
    if isAssemblyExtension ext then
        return! assertFileExists file
    elif isProjectExtension ext then
        do! ProjectResolver.registerMSBuild()
        let! file = assertFileExists file
        return! ProjectResolver.resolveProjectAssembly projectOptions file
    elif isGrammarExtension ext then
        Log.Error("There is no point in listing the precompiled grammars of a grammar file.")
        return! Error()
    else
        Log.Error("Unsupported file extension: {FileExtension}", ext.ToString())
        return! Error()
}

let run json (args: ParseResults<_>) = either {
    let projectOptions = {
        ProjectResolver.Configuration = args.GetResult(Configuration, "Debug")
    }
    let! file =
        args.GetResult InputFile
        |> getAssemblyFile projectOptions

    use loader = new PrecompiledAssemblyFileLoader(file)
    let allGrammarNames = Array.ofSeq loader.Grammars.Keys

    if json then
        JsonSerializer.Serialize(allGrammarNames)
        |> printfn "%s"
    else
        match allGrammarNames with
        | [||] ->
            Log.Information("No precompiled grammars were found.")
        | _ ->
            Log.Information("Found {GrammarCount} precompiled grammars.", allGrammarNames.Length)
            for x in allGrammarNames do
                Log.Information("{GrammarName}", x)
}
