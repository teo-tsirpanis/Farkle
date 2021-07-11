// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.PrecompilerWorker

open Farkle.Tools
open Farkle.Tools.PrecompilerIpcTypes
open Microsoft.Build.Locator
open Microsoft.Build.Utilities
open Serilog
open Sigourney
open System
open System.IO
open System.Text.Json
open System.Threading

let private doIt input =
    let buildMachine = LogSinkBuildMachine()

    let references = input.References |> Array.map AssemblyReference

    let success =
        let loggingHelper = TaskLoggingHelper(buildMachine, "FarklePrecompileTask")
        use logger = LoggerConfiguration().MinimumLevel.Verbose().WriteTo.MSBuild(loggingHelper).CreateLogger()
        let result = Precompiler.precompileAssemblyFromPath CancellationToken.None logger references input.AssemblyPath

        match result with
        | Ok grammars ->
            Weaver.Weave(input.AssemblyPath, input.AssemblyPath,
                (fun asm -> Precompiler.weaveGrammars asm grammars), logger, null, "Farkle.Tools.Precompiler")
            true
        | Error () -> false

    {
        Success = success
        Messages = buildMachine.GetEventsToArray()
    }

/// Returns zero on success or handled errors, and one on unhandled errors.
/// In the latter case the stdout will contain more information about the error.
let private run (argv: ReadOnlySpan<string>) =
    try
        try
            MSBuildLocator.RegisterDefaults() |> ignore
        with
        | :? InvalidOperationException ->
            printfn "%s" ProjectResolver.cannotFindMSBuildMessage
            exit 1
        let inputFile, outputFile =
            if argv.Length = 2 then
                argv.[0], argv.[1]
            else
                printfn "Usage: dotnet tool run farkle <input file> <output file>"
                exit 1
        let input = JsonSerializer.Deserialize<Input>(ReadOnlySpan(File.ReadAllBytes(inputFile)))
        let output = doIt input
        File.WriteAllBytes(outputFile, JsonSerializer.SerializeToUtf8Bytes(output))
        0
    with
    e ->
        printfn "Unhandled exception while running the precompiler worker.\n%O" e
        1

let runIfRequested argv =
    if Array.length argv >= 1 && argv.[0] = "precompiler-worker" then
        run (Span.op_Implicit(argv.AsSpan(1))) |> exit
