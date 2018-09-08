// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Grammar.GOLDParser
open Farkle.Parser

let logger = Log.create "Parser tests"

[<Tests>]
let tests =
    testList "Parser tests" [
        test "A simple mathematical expression can be parsed" {
            let g = EGT.ofFile "simple.egt" |> returnOrFail
            let result = GOLDParser.parseString g (string >> Message.eventX >> logger.info) "111*555"
            match result with
            | Ok x -> x |> AST.toASCIITree |> sprintf "Result: %s" |> Message.eventX |> logger.info
            | Result.Error x -> x |> failtestf "Error: %O"
        }
    ]
