// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarTests

open Chessie.ErrorHandling
open Expecto
open Expecto.Logging
open Farkle.Grammar

let logger = Log.create "My Tests"

[<Tests>]
let tests =
    testList "Grammar tests" [
        test "A legacy CGT grammar fails to be read." {
            let x =
                logger.logSimple (Message.event Info System.Environment.CurrentDirectory)
                match EGT.fromFile "resources/legacy.cgt" with
                | Pass _ | Trial.Warn _ -> []
                | Fail x -> x
            Expect.equal [ReadACGTFile |> EGTReadError] x "Reading the grammar did not fail"
        }
    ]