// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ASTTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Parser
open Generators

let logger = Log.create "AST Tests"

[<Tests>]
let tests =
    testList "AST tests" [
        testProperty "Overkilling AST.simplify does not change it" (fun x ->
            let x: AST<int> = x |> AST.simplify
            x = AST.simplify x)
        ptestProperty "The ASCII tree of an AST and a reduction is the same" (fun x ->
            let redTree = Reduction.drawReductionTree x
            redTree |> Message.eventX |> logger.info
            let astTree = x |> AST.ofReduction |> AST.drawASCIITree
            astTree |> Message.eventX |> logger.info
            redTree = astTree)
    ]
