// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Argu
open Farkle.Tools
open Farkle.Tools.Commands
open Serilog
open Serilog.Events
open System
open System.IO

type FarkleCLIExiter() =
    interface IExiter with
        member _.Name = "Farkle CLI exiter"
        member _.Exit(msg, code) =
            Console.Error.WriteLine(msg)
            exit <| int code

type Arguments =
    | Version
    | [<Inherit>] Json
    | [<Inherit; AltCommandLine("-v"); Unique>] Verbosity of LogEventLevel
    | [<CliPrefix(CliPrefix.None)>] Render of ParseResults<Render.Arguments>
    | [<CliPrefix(CliPrefix.None); Hidden>] New of ParseResults<Render.Arguments>
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<List.Arguments>
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "Display the program's version info."
            | Json -> "Encode output in JSON and print it in a single line in stdout. \
No files will be created and only errors will be logged by default."
            | Verbosity _ -> "Set the verbosity of the tool's logs."
            | New _ | Render _ -> "Render an HTML page or a custom Scriban template from a grammar file."
            | List _ -> "List all precompiled grammars of an assembly."

[<EntryPoint>]
let main argv =
    // The legacy precompiler worker was a special case; it did not use
    // the regular logging mechanism and reported catastrophic exceptions
    // to stderr. That's why it is given its time to shine at the very
    // beginning, even outside Argu.
    if Array.length argv >= 1 && argv[0] = "precompiler-worker" then
        eprintfn "This version of Farkle.Tools does not support the Farkle 6.x precompiler."
        exit 1

    let parser = ArgumentParser.Create("farkle", "Help was requested.", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    let json = results.Contains Json
    let verbosity =
        results.TryGetResult(Verbosity)
        |> Option.defaultValue (if json then LogEventLevel.Error else LogEventLevel.Information)
    Log.Logger <- LoggerConfiguration()
        .MinimumLevel.Is(verbosity)
        .WriteTo.Console()
        .CreateLogger()

    try
        try
            if results.Contains Version then
                Log.Information("Version: {toolsVersion:l}", toolsVersion)
                0
            else
                match results.GetSubCommand() with
                | New args ->
                    Log.Warning("The {Subcommand:l} subcommand is deprecated. Use {Replacement:l} instead.", "new", "render")
                    Render.run json args
                | Render args -> Render.run json args
                | List args -> List.run json args
                | Version | Json | Verbosity _ -> Ok ()
                |> function | Ok () -> 0 | Error () -> 1
        with
        | :? FileNotFoundException as e ->
            Log.Error("File {FileName:l} does not exist.", e.FileName)
            1
        | ex ->
            Log.Fatal(ex, "Exception occured.")
            1
    finally
        Log.CloseAndFlush()
