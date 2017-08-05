// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Chessie.ErrorHandling
open Expecto
open Expecto.Logging
open Farkle.Parser

let logger = Log.create "Parser tests"

[<Tests>]
let tests =
    testList "Parser tests" [
        test "A simple mathematical expression can be parsed" {
            let x = GOLDParser.Parse("resources/simple.egt", "111*555", true) |> GOLDParser.FormatErrors
            let messages = match x with | Ok (_, x) -> x | Bad x -> x
            match x with
            | Ok (x, _) ->
                x |> sprintf "Result: %A" |> Message.eventX |> logger.info
                messages |> List.iter (sprintf "Log: %s" >> Message.eventX >> logger.debug)
            | Bad _ -> messages |> failtestf "Error: %A"
        }
    ]

