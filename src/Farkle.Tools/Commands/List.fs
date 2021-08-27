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
    | [<Unique; MainCommand>] InputFile of string
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-f")>] Framework of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | InputFile _ -> "The assembly or project file from which to look for precompiled grammars."
            | Configuration _ -> "The configuration the project will be evaluated with. The default for most projects is Debug."
            | Framework _ -> "The target framework of the project."

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
        ProjectResolver.Configuration = args.TryGetResult Configuration
        ProjectResolver.TargetFramework = args.TryGetResult Framework
    }
    let! input =
        match args.TryGetResult InputFile with
        | Some input -> Ok input
        | None -> CompositePath.findDefaultProject Environment.CurrentDirectory
    let! resolvedAssembly =
        getAssemblyFile projectOptions input

    let allGrammarNames =
        use loader = new PrecompiledAssemblyFileLoader(resolvedAssembly)
        Array.ofSeq loader.Grammars.Keys

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
