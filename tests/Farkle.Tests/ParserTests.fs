// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle.Parser
open System

let logger = Log.create "Parser tests"

[<Tests>]
let tests =
    testList "Parser tests" [
        test "A simple mathematical expression can be parsed" {
            let (x, messages) = GOLDParser.Parse("resources/simple.egt", "111*555", false) |> GOLDParser.FormatErrors
            messages |> String.concat Environment.NewLine |> Message.eventX |> logger.info
            match x with
            | Choice1Of2 x -> x |> Reduction.drawReductionTree |> sprintf "Result: %s" |> Message.eventX |> logger.info
            | Choice2Of2 messages -> messages |> failtestf "Error: %s"
        }
    ]
