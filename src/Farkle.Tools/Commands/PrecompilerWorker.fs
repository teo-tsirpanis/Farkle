// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.PrecompilerWorker

open Farkle.Tools
open Farkle.Tools.Precompiler
open Farkle.Tools.Precompiler.PrecompilerCommon
open Farkle.Tools.Templating
open Microsoft.Build.Locator
open Microsoft.Build.Utilities
open Serilog
open Serilog.Sinks.MSBuild
open Sigourney
open System
open System.IO
open System.Threading

let private doIt input =
    let buildEngine = LogSinkBuildEngine(input.TaskLineNumber, input.TaskColumnNumber, input.TaskProjectFile)
    let references = input.References |> Array.map AssemblyReference
    let generatedConflictReports = ResizeArray()

    let loggingHelper = TaskLoggingHelper(buildEngine, "FarklePrecompileTask")
    use loggerObj =
        LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.MSBuild(loggingHelper)
            .CreateLogger()
    // Sigourney's Serilog logger automatically includes this.
    let logger =
        loggerObj
            .ForContext(MSBuildProperties.File, input.AssemblyPath)
    let outputDir = Path.GetDirectoryName input.AssemblyPath
    let fCreateConflictReport = TemplateEngine.createConflictReport generatedConflictReports logger outputDir
    let results =
        PrecompilerInProcess.precompileAssemblyFromPath
            CancellationToken.None logger fCreateConflictReport input.ErrorMode references input.AssemblyPath

    if generatedConflictReports.Count <> 0 then
        logger.Information(conflictReportHint)

    if not loggingHelper.HasLoggedErrors then
        let fWeave = Func<_,_>(fun asm -> PrecompilerInProcess.weaveGrammars asm results)
        Weaver.Weave(input.AssemblyPath, null, fWeave, logger, null, weaverName)

    {
        Messages = buildEngine.GetEventsToArray()
        GeneratedConflictReports = generatedConflictReports.ToArray()
    }

/// Returns zero on success or handled errors, one on unhandled errors
/// and two on invalid command line arguments.
/// In the latter case stdout will contain more information about the error.
let private run argv =
    try
        try
            MSBuildLocator.RegisterDefaults() |> ignore
        with
        | :? InvalidOperationException ->
            eprintfn "%s" ProjectResolver.cannotFindMSBuildMessage
            exit 1

        match argv with
        | [|_verb; version; _; _|] when version <> ipcProtocolVersion ->
            eprintfn "Precompiler worker protocol mismatch."
            1
        | [|_verb; _; inputFile; outputFile|] ->
            let input = readFromJsonFile<PrecompilerWorkerInput> inputFile
            let output = doIt input
            writeToJsonFile outputFile output
            0
        | _ ->
            eprintfn "Usage: dotnet tool run farkle -- precompiler-worker %s <input file> <output file>" ipcProtocolVersion
            2
    with
    e ->
        eprintfn "Unhandled exception while running the precompiler worker:\n%O" e
        1

let runIfRequested argv =
    if Array.length argv >= 1 && argv.[0] = "precompiler-worker" then
        run argv |> exit
