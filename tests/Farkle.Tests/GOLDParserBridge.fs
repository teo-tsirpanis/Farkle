// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GOLDParserBridge

open Expecto
open Farkle.Builder
open Farkle.Grammar
open System
open System.Diagnostics
open System.IO

let private createGML (x: GrammarDefinition) =
    let (Nonterminal(_, startSymbolName)) = x.StartSymbol
    [
        yield sprintf "\"Start Symbol\" = <%s>" startSymbolName
        yield! x.Productions |> Seq.map string
    ]
    |> String.concat Environment.NewLine

let private getGOLDBuildPath() =
    match Environment.GetEnvironmentVariable "GOLD_BUILD" with
    | null -> skiptest "The GOLD Parser Builder is not installed in this machine. \
Please set the GOLD_BUILD environment variable to the path of the CLI builder's executable."
    | goldPath -> goldPath

let private callGOLDParserBuilder gml =
    let goldPath = getGOLDBuildPath()
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
        EGT.ofFile egtPath
    else
        let log = File.ReadAllText(logPath)
        failwithf "Building the grammar failed: %s" log

let checkIfGOLDExists() = getGOLDBuildPath() |> ignore

let buildUsingGOLDParser grammar =
    let gml = createGML grammar
    callGOLDParserBuilder gml
