// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

module Farkle.Tests.IelrTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Diagnostics.Builder

[<Tests>]
let tests = testList "IELR(1) tests" [
    test "Simple non-LALR(1) grammar" {
        let grammar =
            let A = "A" |||= [!& "c"]
            let B = "B" |||= [!& "c"]

            "S" |||= [
                !& "a" .>> A .>> "d"
                !& "b" .>> B .>> "d"
                !& "a" .>> B .>> "e"
                !& "b" .>> A .>> "e"
            ]
            |> _.AutoWhitespace(false)

        let (grammarLalr, diagnostics) = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1) |> buildWithWarnings
        Expect.isTrue grammarLalr.LrStateMachine.HasConflicts "Building with LALR(1) should have had conflicts"
        Expect.all diagnostics (fun x -> match x.Message with :? LrConflict as x -> x.Kind = LrConflictKind.ReduceReduce | _ -> false) "Expected all diagnostics to be Reduce-Reduce conflicts"

        let resultIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1).BuildSyntaxCheck()
        Expect.isFalse resultIelr.IsFailing "Building with IELR(1) failed"

        expectIsParseSuccess (resultIelr.Parse "acd") "Parsing 'acd' failed"
        expectIsParseSuccess (resultIelr.Parse "bcd") "Parsing 'bcd' failed"
        expectIsParseSuccess (resultIelr.Parse "ace") "Parsing 'ace' failed"
        expectIsParseSuccess (resultIelr.Parse "bce") "Parsing 'bce' failed"
    }
]
