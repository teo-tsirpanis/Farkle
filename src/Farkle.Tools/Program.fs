// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Argu
open Farkle.Tools
open Farkle.Tools.Commands
open System

type FarkleCLIExiter() =
    interface IExiter with
        member __.Name = "Farkle CLI exiter"
        member __.Exit(msg, code) =
            Console.Error.WriteLine(msg)
            exit <| int code

type Arguments =
    | [<Inherit>] Version
    | [<CliPrefix(CliPrefix.None)>] New of ParseResults<New.Arguments>
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "display the program's version info."
            | New _ -> "generate a skeleton program from a grammar file and a Scriban template."

[<EntryPoint>]
let main _ =
    let parser = ArgumentParser.Create("farkle", "Help was requested", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    if results.Contains <@ Version @> then
        Console.WriteLine toolsVersion
    else
        match results.GetSubCommand() with
        | New args -> New.run args
        | _ -> ()
    0
