// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RangeMapTests

open Expecto
open Farkle.Collections
open Farkle.Tests
open FsCheck

[<Tests>]
let tests =
    testList "RangeMap tests" [
        test "The empty RangeMap is empty" {
            let rm: RangeMap<int,int> = RangeMap.empty()
            Expect.isTrue (RangeMap.isEmpty rm) "The empty RangeMap is not empty"
        }

        testProperty "The empty RangeMap does not contain anything" (fun (x: int) ->
            let rm = RangeMap.empty()
            RangeMap.tryFind x rm |> ValueOption.isNone)

        test "Overlapping ranges are not accepted" {
            Expect.isNone (RangeMap.ofRanges [|[|4, 8|], (); [|5, 30|], ()|]) "A RangeMap with overlapping ranges was created"
        }

        test "Single elements inside ranges are not accepted" {
            Expect.isNone (RangeMap.ofRanges [|[|9,9|], (); [|2,100|], ()|]) "A RangeMap with a single element inside a range was created"
        }

        testProperty "An array with distinct elements is valid"
            (Seq.ofList >> Seq.distinct >> Seq.map (fun (x: int) -> [|(x, x)|], ()) >> Array.ofSeq >> RangeMap.ofRanges >> Option.isSome)

        // A boolean value type makes continuous spaces more likely.
        testProperty "RangeMap.ofSeq is the inverse of RangeMap.toSeq" (fun (x: RangeMap<int, bool>) ->
            let xs = RangeMap.toSeq x
            let x' = RangeMap.ofSeq xs
            let xs' = RangeMap.toSeq x'
            // We have to compare the sequences, not the RangeMaps, because
            // RangeMap.ofSeq might create an equivalent RangeMap with less elements.
            Expect.sequenceEqual xs xs' "The RangeMaps are different.")
    ]
