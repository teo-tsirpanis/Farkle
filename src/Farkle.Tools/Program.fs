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
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "Display the program's version info."
            | Verbosity _ -> "Set the verbosity of the tool's logs."
            | New _ -> "Generate a skeleton program from a grammar file and a Scriban template."

[<EntryPoint>]
let main _ =
    let parser = ArgumentParser.Create("farkle", "Help was requested", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    let verbosity = results.GetResult(Verbosity, Events.LogEventLevel.Information)
    Log.Logger <- LoggerConfiguration()
        .MinimumLevel.Is(verbosity)
        .WriteTo.Console()
        .CreateLogger()
    if results.Contains Version then
        Log.Information("Version: {toolsVersion}", toolsVersion)
        0
    else
        match results.GetSubCommand() with
        | New args -> New.run args
        | _ -> Ok ()
        |> function | Ok () -> 0 | Error () -> 1
