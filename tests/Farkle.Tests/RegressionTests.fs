// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegressionTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Parser

let private reproduceIssue issueNumber = test (sprintf "GitHub issue #%02i" issueNumber)
let private freproduceIssue issueNumber = ftest (sprintf "GitHub issue #%02i" issueNumber)

let parse rf str = RuntimeFarkle.parseString rf str

[<Tests>]
let tests = testList "Regression tests" [
    reproduceIssue 8 {
        let rf = loadRuntimeFarkle "issue-8.egt"
        Expect.isOk (parse rf "45") "The two-digit input was not successfully parsed"

        let expectedError =
            ParserError(Position.Create 1UL 2UL 1UL, ParseErrorType.UnexpectedEndOfInput)
            |> FarkleError.ParseError
            |> Error

        Expect.equal (parse rf "3") expectedError
            "The issue was reproduced; parsing a single-digit input was successful, while it shouldn't"
    }
    
    reproduceIssue 301 {
        let opt_D = "opt_D" |||= [
            empty
            !& "d"
        ]

        let opt_CD = "opt_CD" |||= [
            !& "c"
            !% opt_D
        ]

        let opt_B = "opt_B" |||= [
            empty
            !& "b"
        ]

        let root = "root" |||= [
            !& "a" .>> opt_B .>> opt_CD .>> "e"
        ]

        let rf = RuntimeFarkle.buildUntyped root
        
        Expect.isOk (parse rf "ae") "Parsing \"ae\" should have succeeded"
        Expect.isOk (parse rf "abe") "Parsing \"abe\" should have succeeded"
    }
]
