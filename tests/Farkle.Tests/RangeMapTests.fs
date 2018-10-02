// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RangeMapTests

open Expecto
open Farkle
open Farkle.Collections

[<Tests>]
let tests =
    testList "RangeMap tests" [
        test "The empty RangeMap is empty" {
            Expect.isTrue (RangeMap.isEmpty RangeMap.empty) "The empty RangeMap is not empty"
        }

        testProperty "The empty RangeMap does not contain anything"
            ((*) 1 >> flip RangeMap.tryFind RangeMap.empty >> Option.isNone)

        test "Overlapping ranges are not accepted" {
            Expect.isNone (RangeMap.ofRanges [|[|4, 8|], (); [|5, 30|], ()|]) "A RangeMap with overlapping ranges was created"
        }

        test "Single elements inside ranges are not accepted" {
            Expect.isNone (RangeMap.ofRanges [|[|9,9|], (); [|2,100|], ()|]) "A RangeMap with a single element inside a range was created"
        }

        testProperty "A sorted array with distinct elements is valid"
            (Seq.ofList >> Seq.distinct >> Seq.map (fun (x: int) -> [|(x, x)|], ()) >> Array.ofSeq >> RangeMap.ofRanges >> Option.isSome)
    ]