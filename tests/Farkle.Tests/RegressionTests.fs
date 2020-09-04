// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegressionTests

open Expecto
open Farkle
open Farkle.Parser

let private reproduceIssue issueNumber = test (sprintf "GitHub issue #%02i" issueNumber)

let parse rf str = RuntimeFarkle.parseString rf str

[<Tests>]
let tests = testList "Regression tests" [
    reproduceIssue 8 {
        let rf = loadRuntimeFarkle "issue-8.egt"
        Expect.isOk (parse rf "45") "The two-digit input was not successfully parsed"
        Expect.equal (parse rf "3")
            (ParserError(Position.Initial.Advance '3', ParseErrorType.UnexpectedEndOfInput) |> FarkleError.ParseError |> Result.Error)
            "The issue was reproduced; parsing a single-digit input was successful, while it shouldn't"
    }
]
