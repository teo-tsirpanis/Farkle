// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.List

open Argu
open Farkle.Monads.Either
open Farkle.Tools
open Serilog
open System.Text.Json

type Arguments =
    | [<ExactlyOnce; MainCommand>] AssemblyFile of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | AssemblyFile _ -> "The assembly file from which to look for precompiled grammars."

let run json (args: ParseResults<_>) = either {
    let! file =
        args.GetResult AssemblyFile
        |> assertFileExists

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
