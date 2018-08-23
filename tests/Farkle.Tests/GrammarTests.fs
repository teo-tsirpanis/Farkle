// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarTests

open Expecto
open Expecto.Logging
open Farkle.Grammar
open Farkle.Grammar.GOLDParser

let logger = Log.create "Farkle tests"

[<Tests>]
let tests =
    testList "Grammar tests" [
        test "A legacy CGT grammar fails to be read." {
            let x = EGT.ofFile "legacy.cgt"
            Expect.equal x (Result.Error ReadACGTFile) "Reading the grammar did not fail"
        }

        test "A new grammar is successfuly read" {
            let x = EGT.ofFile "simple.egt"
            match x with
            | Ok x -> x |> sprintf "Generated grammar: %A" |> Message.eventX |> logger.debug
            | Result.Error x -> failtestf "Test failed: %A" x
        }
    ]