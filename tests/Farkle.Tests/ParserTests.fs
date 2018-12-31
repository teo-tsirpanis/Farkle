// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.PostProcessor

let logger = Log.create "Parser tests"

[<Tests>]
let tests =
    testList "Parser tests" [
        test "A simple mathematical expression can be parsed" {
            let rf = RuntimeFarkle.createFromPostProcessor PostProcessor.syntaxCheck "simple.egt"
            let result = RuntimeFarkle.parseString rf (string >> Message.eventX >> logger.info) "111*555"
            match result with
            | Ok () -> ()
            | Result.Error x -> failtestf "Error: %O" x
        }
    ]
