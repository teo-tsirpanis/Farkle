// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ASTTests

open Expecto
open Expecto.Logging
open Farkle
open Generators

let logger = Log.create "AST Tests"

[<Tests>]
let tests =
    testList "AST tests" [
        testProperty "Overkilling AST.simplify does not change it" (fun x ->
            let x = x |> AST.simplify
            x = AST.simplify x)
    ]
