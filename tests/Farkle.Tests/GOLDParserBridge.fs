// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GOLDParserBridge

open Expecto
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open System
open System.Collections.Immutable
open System.Diagnostics
open System.IO

type GrammarDefinition = GrammarDefinition of startSymbol: Nonterminal * productions: ImmutableArray<Production>

let private createGML (GrammarDefinition(startSymbol, productions)) =
    [
        yield sprintf "\"Start Symbol\" = <%s>" startSymbol.Name
        yield! productions |> Seq.map string
    ]
    |> String.concat Environment.NewLine

let private callGOLDParserBuilder gml =
    match Environment.GetEnvironmentVariable "GOLD_BUILD" with
    | null -> skiptest "The GOLD Parser Builder is not installed in this machine. \
Please set the GOLD_BUILD environment variable to the path of the CLI builder's executable."
    | goldPath ->
        let grammarPath = Path.GetTempFileName()
        use __ = {new IDisposable with member __.Dispose() = File.Delete(grammarPath)}
        let egtPath = Path.ChangeExtension(grammarPath, ".egt")
        use __ = {new IDisposable with member __.Dispose() = File.Delete(egtPath)}
        let logPath = Path.ChangeExtension(grammarPath, ".log")
        use __ = {new IDisposable with member __.Dispose() = File.Delete(logPath)}
        File.WriteAllText(grammarPath, gml)
        use builderProcess =
            let startInfo = ProcessStartInfo()
            startInfo.FileName <- goldPath
            startInfo.Arguments <- sprintf "%s %s %s" grammarPath egtPath logPath
            Process.Start(startInfo)
        builderProcess.WaitForExit()
        if File.Exists(egtPath) then
            match EGT.ofFile egtPath with
            | Ok grammar -> grammar
            | Error x -> failwithf "Reading the generated EGT file failed: %O" x
        else
            let log = File.ReadAllText(logPath)
            failwithf "Building the grammar failed: %s" log

let buildUsingGOLDParser grammar =
    let gml = createGML grammar
    callGOLDParserBuilder gml
