// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RangeMapTests

open Expecto
open Farkle.Collections

[<Tests>]
let tests =
    testList "RangeMap tests" [
        test "The empty RangeMap is empty" {
            Expect.isTrue (RangeMap.isEmpty RangeMap.empty) "The empty RangeMap is not empty"
        }
    ]