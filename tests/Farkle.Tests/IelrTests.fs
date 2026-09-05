// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

module Farkle.Tests.IelrTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence
open Farkle.Diagnostics.Builder

[<Tests>]
let tests = testList "IELR(1) tests" [
    test "IELR(1) is the default" {
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

        let resultIelr = grammar.BuildSyntaxCheck()
        Expect.isFalse resultIelr.IsFailing "Building with IELR(1) failed"
    }

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

        let grammarLalr, diagnostics = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1) |> buildWithWarnings
        Expect.isTrue grammarLalr.LrStateMachine.HasConflicts "Building with LALR(1) should have had conflicts"
        Expect.all diagnostics (fun x -> match x.Message with :? LrConflict as x -> x.Kind = LrConflictKind.ReduceReduce | _ -> false) "Expected all diagnostics to be Reduce-Reduce conflicts"

        let resultIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1).BuildSyntaxCheck()
        Expect.isFalse resultIelr.IsFailing "Building with IELR(1) failed"
        let grammarIelr = resultIelr.GetGrammar()
        Expect.isGreaterThan grammarIelr.LrStateMachine.Count grammarLalr.LrStateMachine.Count "IELR(1) should have more states than LALR(1)"

        expectIsParseSuccess (resultIelr.Parse "acd") "Parsing 'acd' failed"
        expectIsParseSuccess (resultIelr.Parse "bcd") "Parsing 'bcd' failed"
        expectIsParseSuccess (resultIelr.Parse "ace") "Parsing 'ace' failed"
        expectIsParseSuccess (resultIelr.Parse "bce") "Parsing 'bce' failed"
    }

    test "IELR preserves language lost to an invasive conflict" {
        // Figure 1 from IELR paper
        let grammar =
            let A = "A" |||= [!& "a"; !& "a" .>> "a"]

            "S" |||= [
                !& "a" .>> A .>> "a"
                !& "b" .>> A .>> "b"
            ]
            |> _.AutoWhitespace(false)
            |> _.WithOperatorScope(OperatorScope(LeftAssociative("a")))

        let resultLalr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1).BuildSyntaxCheck()
        Expect.isFalse resultLalr.IsFailing "LALR(1) should build with the declared conflict resolution"
        expectIsParseFailure (resultLalr.Parse "baab") "LALR(1) should not have accepted 'baab'"

        let resultIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1).BuildSyntaxCheck()
        Expect.isFalse resultIelr.IsFailing "IELR(1) failed to build"
        expectIsParseSuccess (resultIelr.Parse "baab") "IELR(1) failed to preserve 'baab'"
    }

    test "IELR splits mutated reduce-reduce conflicts" {
        // Figure 3 from IELR paper
        let grammar =
            let A = "A" |||= [!& "a" .>> "a"]
            let B = "B" |||= [!& "a" .>> "a"]
            let C = "C" |||= [!& "a" .>> "a"]

            "S" |||= [
                !& "a" .>> A .>> "a"
                !& "a" .>> B .>> "a"
                !& "a" .>> C .>> "a"
                !& "b" .>> A .>> "b"
                !& "b" .>> B .>> "a"
                !& "b" .>> C .>> "a"
            ]
            |> _.AutoWhitespace(false)

        let grammarLalr, diagnosticsLalr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1) |> buildWithWarnings
        Expect.isTrue grammarLalr.LrStateMachine.HasConflicts "LALR(1) should have reduce-reduce conflicts"
        Expect.hasLength diagnosticsLalr 2 "LALR(1) should report a single 3-way reduce-reduce conflict"

        let grammarIelr, diagnosticsIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1) |> buildWithWarnings
        Expect.isTrue grammarIelr.LrStateMachine.HasConflicts "IELR(1) should retain unresolved reduce-reduce conflicts"
        Expect.isGreaterThan grammarIelr.LrStateMachine.Count grammarLalr.LrStateMachine.Count "IELR(1) should split the mutated conflict"
        Expect.hasLength diagnosticsIelr 3 "IELR(1) should expose the split conflict manifestations"
    }

    test "Simple non-LALR(1) grammar with nullable prefix" {
        let grammar =
            let A = "A" |||= [!& "c"]
            let B = "B" |||= [!& "c"]
            let X = "X" |||= [empty; !& "f"]

            "S" |||= [
                !& "a" .>> X .>> A .>> "d"
                !& "b" .>> X .>> B .>> "d"
                !& "a" .>> X .>> B .>> "e"
                !& "b" .>> X .>> A .>> "e"
            ]
            |> _.AutoWhitespace(false)

        let grammarLalr, diagnostics = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1) |> buildWithWarnings
        Expect.isTrue grammarLalr.LrStateMachine.HasConflicts "Building with LALR(1) should have had conflicts"
        Expect.all diagnostics (fun x -> match x.Message with :? LrConflict as x -> x.Kind = LrConflictKind.ReduceReduce | _ -> false) "Expected all diagnostics to be Reduce-Reduce conflicts"

        let resultIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1).BuildSyntaxCheck()
        Expect.isFalse resultIelr.IsFailing "Building with IELR(1) failed"
        let grammarIelr = resultIelr.GetGrammar()
        Expect.isGreaterThan grammarIelr.LrStateMachine.Count grammarLalr.LrStateMachine.Count "IELR(1) should have more states than LALR(1)"

        expectIsParseSuccess (resultIelr.Parse "acd") "Parsing 'acd' failed"
        expectIsParseSuccess (resultIelr.Parse "bcd") "Parsing 'bcd' failed"
        expectIsParseSuccess (resultIelr.Parse "ace") "Parsing 'ace' failed"
        expectIsParseSuccess (resultIelr.Parse "bce") "Parsing 'bce' failed"

        expectIsParseSuccess (resultIelr.Parse "afcd") "Parsing 'afcd' failed"
        expectIsParseSuccess (resultIelr.Parse "bfcd") "Parsing 'bfcd' failed"
        expectIsParseSuccess (resultIelr.Parse "afce") "Parsing 'afce' failed"
        expectIsParseSuccess (resultIelr.Parse "bfce") "Parsing 'bfce' failed"
    }

    test "Ambiguous grammar" {
        let grammar =
            let S = nonterminalU "S"
            setProductionsU S [
                !% S .>> "+" .>> S
                !% S .>> "*" .>> S
                !& "x"
            ]
            S
            |> _.AutoWhitespace(false)

        let grammarLalr, diagnosticsLalr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Lalr1) |> buildWithWarnings
        Expect.isTrue grammarLalr.LrStateMachine.HasConflicts "Building with LALR(1) should have had conflicts"
        Expect.all diagnosticsLalr (fun x -> x.Code = "FARKLE0007") "Not all diagnostics are conflicts"

        let grammarIelr, diagnosticsIelr = grammar.WithParserGenerationAlgorithm(ParserGenerationAlgorithm.Ielr1) |> buildWithWarnings
        Expect.isTrue grammarIelr.LrStateMachine.HasConflicts "Building with IELR(1) should have had conflicts"
        Expect.all diagnosticsIelr (fun x -> x.Code = "FARKLE0007") "Not all diagnostics are conflicts"

        Expect.hasLength grammarIelr.LrStateMachine grammarLalr.LrStateMachine.Count "The IELR(1) grammar should have the same number of states as the LALR(1) grammar"
        Expect.hasLength diagnosticsIelr diagnosticsLalr.Count "The IELR(1) grammar should have the same number of diagnostics as the LALR(1) grammar"
    }
]
