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
            (lb :> ICollection<_>).Add x
        Expect.sequenceEqual lb xs "The list builder's contents are different"
        let xsNew = lb.MoveToList()
        Expect.sequenceEqual xsNew xs "The generated list's is different"
        Expect.isEmpty lb "The list builder was not cleared after the list was created")
]
