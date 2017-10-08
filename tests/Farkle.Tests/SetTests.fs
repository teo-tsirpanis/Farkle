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
        testProperty "Converting back and forth between F# character sets and range sets is lossless" (fun x ->
            x |> RangeSet.ofCharSet |> RangeSet.toCharSet |> ((=) x)
        )
        
        testProperty "If an F# set contains an item, its range set also contains it." (fun set x ->
            (Set.contains x set) ==> (set |> RangeSet.ofCharSet |> flip RangeSet.contains x)
        )
    ]
