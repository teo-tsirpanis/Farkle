// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Argu
open Farkle.Tools
open Farkle.Tools.Commands
open Serilog
open System

type FarkleCLIExiter() =
    interface IExiter with
        member __.Name = "Farkle CLI exiter"
        member __.Exit(msg, code) =
            Console.Error.WriteLine(msg)
            exit <| int code

type Arguments =
    | Version
    | [<Inherit; AltCommandLine("-v"); Unique>] Verbosity of Events.LogEventLevel
    | [<CliPrefix(CliPrefix.None)>] New of ParseResults<New.Arguments>
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<Build.Arguments>
    | [<AltCommandLine("generate-predefined-sets"); Hidden>] GeneratePredefinedSets of ParseResults<GeneratePredefinedSets.Arguments>
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "Display the program's version info."
            | Verbosity _ -> "Set the verbosity of the tool's logs."
            | New _ -> "Generate a skeleton program from a grammar file and a Scriban template."
            | Build _ -> "Precompiles the grammars of the designtime Farkles of an assembly, \
to speed-up building time. To enable that, use the RuntimeFarkle.markForPercompile fuction or \
the MarkForPrecompile extension method."
            | GeneratePredefinedSets _ -> "Generate an F# source file with GOLD Parser's predefined sets. \
For internal use only."

[<EntryPoint>]
let main _ =
    let parser = ArgumentParser.Create("farkle", "Help was requested", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    let verbosity = results.GetResult(Verbosity, Events.LogEventLevel.Information)
    Log.Logger <- LoggerConfiguration()
        .MinimumLevel.Is(verbosity)
        .WriteTo.Console()
        .CreateLogger()

    use __ = {new IDisposable with member __.Dispose() = Log.CloseAndFlush()}
    try
        if results.Contains Version then
            Log.Information("Version: {toolsVersion}", toolsVersion)
            0
        else
            match results.GetSubCommand() with
            | New args -> New.run args
            | Build args -> Build.run args
            | GeneratePredefinedSets args -> GeneratePredefinedSets.run args
            | Version _ | Verbosity _ -> Ok ()
            |> function | Ok () -> 0 | Error () -> 1
    with
    | ex ->
        Log.Fatal(ex, "Exception occured")
        1
