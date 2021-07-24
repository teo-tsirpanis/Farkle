// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.PrecompilerWorker

open Farkle.Tools
open Farkle.Tools.Precompiler
open Farkle.Tools.Precompiler.PrecompilerCommon
open Microsoft.Build.Locator
open Microsoft.Build.Utilities
open Serilog
open Sigourney
open System
open System.Threading

let private doIt input =
    let buildMachine = LogSinkBuildMachine(input.TaskLineNumber, input.TaskColumnNumber, input.TaskProjectFile)

    let references = input.References |> Array.map AssemblyReference

    let success =
        let loggingHelper = TaskLoggingHelper(buildMachine, "FarklePrecompileTask")
        use logger =
            LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.MSBuild(loggingHelper)
                .CreateLogger()
        let result = PrecompilerInProcess.precompileAssemblyFromPath CancellationToken.None logger references input.AssemblyPath

        match result with
        | Ok grammars ->
            let fWeave = Func<_,_>(fun asm -> PrecompilerInProcess.weaveGrammars asm grammars)
            Weaver.Weave(input.AssemblyPath, null, fWeave, logger, null, weaverName)
            true
        | Error () -> false

    {
        Success = success
        Messages = buildMachine.GetEventsToArray()
    }

/// Returns zero on success or handled errors, one on unhandled errors
/// and two on invalid command line arguments.
/// In the latter case the stdout will contain more information about the error.
let private run argv =
    try
        try
            MSBuildLocator.RegisterDefaults() |> ignore
        with
        | :? InvalidOperationException ->
            eprintfn "%s" ProjectResolver.cannotFindMSBuildMessage
            exit 1
        let inputFile, outputFile =
            match argv with
            | [|_verb; version; _; _|] when version <> ipcProtocolVersion ->
                eprintfn "Precompiler worker protocol mismatch."
                exit 1
            | [|_verb; _; x1; x2|] -> x1, x2
            | _ ->
                eprintfn "Usage: dotnet tool run farkle -- precompiler-worker %s <input file> <output file>" ipcProtocolVersion
                exit 2
        let input = readFromJsonFile<PrecompilerWorkerInput> inputFile

        let output = doIt input

        writeToJsonFile outputFile output
        0
    with
    e ->
        eprintfn "Unhandled exception while running the precompiler worker.\n%O" e
        1

let runIfRequested argv =
    if Array.length argv >= 1 && argv.[0] = "precompiler-worker" then
        run argv |> exit
