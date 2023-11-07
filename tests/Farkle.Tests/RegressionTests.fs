// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegressionTests

open Expecto
open Farkle
open Farkle.Diagnostics

let private reproduceIssue issueNumber = test (sprintf "GitHub issue #%02i" issueNumber)

[<Tests>]
let tests = testList "Regression tests" [
    reproduceIssue 8 {
        let parser = loadCharParser "issue-8.egt"
        let result = CharParser.parseString parser "45"
        Expect.equal result (ParserResult.CreateSuccess()) "The two-digit input was not successfully parsed"

        let (|TextPosition|) (pos: TextPosition) = TextPosition(pos.Line, pos.Column)
        let result = CharParser.parseString parser "3"
        match result with
        | ParserError(ParserDiagnostic(TextPosition(1, 1), LexicalError _)) -> ()
        | _ -> failtestNoStackf $"The issue was not reproduced: {result}"
    }
]
