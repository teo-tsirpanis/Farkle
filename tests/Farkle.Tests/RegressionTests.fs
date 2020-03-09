// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegressionTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Parser
open Farkle.PostProcessor

let logger = Log.create "Regression tests"

let private reproduceIssue issueNumber = test (sprintf "GitHub issue #%02i" issueNumber)

let parse rf str = RuntimeFarkle.parseString rf (string >> Message.eventX >> logger.verbose) str

[<Tests>]
let tests = testList "Regression tests" [
    reproduceIssue 8 {
        let rf = loadRuntimeFarkle "issue-8.egt" |> RuntimeFarkle.changePostProcessor PostProcessor.syntaxCheck
        Expect.isOk (parse rf "45") "The two-digit input was not successfully parsed"
        Expect.equal (parse rf "3")
            (Message(Position.Initial.Advance '3', ParseErrorType.LexicalError '\000') |> FarkleError.ParseError |> Result.Error)
            "The issue was reproduced; parsing a single-digit input was successful, while it shouldn't"
    }
]
