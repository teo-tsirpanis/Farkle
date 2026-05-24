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
open System.Reflection.PortableExecutable
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
        return file
    elif isProjectExtension ext then
        do! ProjectResolver.registerMSBuild()
        return! ProjectResolver.resolveProjectAssembly projectOptions file
    elif isGrammarExtension ext then
        Log.Error("There is no point in listing the precompiled grammars of a grammar file.")
        return! Error()
    else
        Log.Error("Unsupported file extension {FileExtension:l}.", ext.ToString())
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

    let allGrammars =
        use f = File.OpenRead resolvedAssembly
        use pe = new PEReader(f)
        PrecompiledAssemblyFileLoader.loadAll pe
        |> Seq.map (fun g ->
            let grammar = g.LoadGrammar()
            {|
                ContainingTypeName = PrecompiledAssemblyFileLoader.getTypeFullName g
                Name = grammar.GrammarInfo.Name
                Size = g.Size
                Key = g.Key
            |}
        )
        |> Array.ofSeq
        |> fun xs -> xs |> Array.sortInPlaceBy (fun x -> x.ContainingTypeName); xs

    if json then
        JsonSerializer.Serialize allGrammars
        |> printfn "%s"
    else
        if Array.isEmpty allGrammars then
            Log.Information "No precompiled grammars were found."
        for x in allGrammars do
            let mapIfHasValue f x = if x = "" then x else f x
            let key = x.Key |> mapIfHasValue (sprintf "::%s")
            printfn "%s%s: Name = %s, Size = %d" x.ContainingTypeName key x.Name x.Size
}
