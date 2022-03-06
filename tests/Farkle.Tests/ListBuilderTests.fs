// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ListBuilderTests

open Expecto
open Farkle.Collections
open System.Collections.Generic

[<Tests>]
let tests = testList "List builder tests" [
    testProperty "List builders can recreate a list" (fun (xs: int list) ->
        let lb = ListBuilder()
        for x in xs do
            lb.Add x
        let xsNew = lb.MoveToList()
        Expect.sequenceEqual xsNew xs "The generated lists are different")
]
