// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.VirtualTerminalTests

open Expecto
open Farkle
open Farkle.Parser
open Farkle.Samples.IndentBased

[<Tests>]
let tests = testList "Virtual terminal tests" [
    test "A sample IndentCode file is successfully parsed" {
        let code = Block [Line "USS Oriskany"; Block [Line "CV"; Line "34"]]
        let rendered = "
USS Oriskany

    CV

    34
    "

        let result = runtime.Parse rendered

        Expect.equal result (Ok code) "The parsed IndentCode is different from the original"
    }

    test "An IndentCode file with invalid indentation fails to be parsed" {
        let code = "A\n    B\n   C"
        let result = runtime.Parse code
        let expectedResult =
            ParserError(
                Position.Create 3UL 1UL 8UL,
                ParseErrorType.UserError "unindent does not match any outer indentation level")
            |> FarkleError.ParseError
            |> Error

        Expect.equal result expectedResult "Parsing failed with a different kind of error"
    }
]
