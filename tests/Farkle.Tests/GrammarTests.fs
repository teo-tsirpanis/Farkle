// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarTests

open Chessie.ErrorHandling
open Expecto
open Expecto.Logging
open Farkle.Grammar

let logger = Log.create "Farkle tests"

[<Tests>]
let tests =
    testList "Grammar tests" [
        test "A legacy CGT grammar fails to be read." {
            let x =
                match EGT.fromFile "resources/legacy.cgt" with
                | Pass _ | Trial.Warn _ -> []
                | Fail x -> x
            Expect.equal [ReadACGTFile |> EGTReadError] x "Reading the grammar did not fail"
        }

        test "A new grammar is successfuly read" {
            let x = EGT.fromFile "resources/simple.egt"
            match x with
            | Ok (x, _) -> x |> sprintf "Generated grammar: %A" |> Message.eventX |> logger.info
            | Bad x -> failtestf "Test failed: %A" x
        }
    ]