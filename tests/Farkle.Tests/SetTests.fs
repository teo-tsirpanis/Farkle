// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.SetTests

open Expecto
open FsCheck
open Farkle
open Generators

[<Tests>]
let tests =
    testList "Set tests" [
        testProperty "Converting back and forth between F# sets and range sets is lossless" (fun x ->
            x |> SetUtils.setToRanges |> SetUtils.rangesToSet |> ((=) x)
        )

        testProperty "If an F# set contains an item, its range set also contains it." (fun set x ->
                set |> Set.add x |> SetUtils.setToRanges |> RangeSet.contains x
        )
    ]
