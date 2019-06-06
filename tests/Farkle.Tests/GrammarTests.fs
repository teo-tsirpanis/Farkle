// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Grammar
open Farkle.Grammar.GOLDParser

let logger = Log.create "Grammar tests"

[<Tests>]
let tests =
    testList "Grammar tests" [
        test "A legacy CGT grammar fails to be read." {
            let x = EGT.ofFile "../resources/legacy.cgt"
            Expect.equal x (Result.Error ReadACGTFile) "Reading the grammar did not fail"
        }

        test "A new grammar is successfuly read" {
            let x = EGT.ofFile "../resources/simple.egt"
            match x with
            | Ok x -> x |> sprintf "Generated grammar: %A" |> Message.eventX |> logger.debug
            | Result.Error x -> failtestf "Test failed: %A" x
        }

        test "Terminal naming works properly" {
            let fTerminal (term, expected) =
                (1u, term)
                |> Terminal
                |> string
                |> Flip.Expect.equal "String value of a terminal is not the same as expected." expected
            [
                "'", "''"
                ".NET", "'.NET'"
                ".NET Core", "'.NET Core'"
                "Number", "Number"
                "*", "'*'"
                "starts.with_a-letter", "starts.with_a-letter"
                "#134<=>#642", "'#134<=>#642'"
            ] |> List.iter fTerminal
        }
    ]
