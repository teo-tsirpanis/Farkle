// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.OperatorPrecedenceTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence

[<Tests>]
let tests = testList "Operator precedence tests" [
    test "The calculator respects operator precedence and associativity" {
        let runtime = SimpleMaths.int
        let testData = [
            "5 * 5 - 25", 0
            "6 / 2 * (1 + 2)", 9
            "125 / 25 / 5", 1
        ]
        for expr, result in testData do
            Expect.equal (runtime.Parse expr) (Ok result) (sprintf "Parsing '%s' failed" expr)
    }

    test "Precedence among symbols from different groups cannot be compared" {
        let grammar =
            let expr = nonterminalU "Expr"

            let opGroup1 = OperatorGroup(LeftAssociative("+"))
            let opGroup2 = OperatorGroup(LeftAssociative("-"))

            let number =
                Terminals.int "Number"
                |> DesigntimeFarkle.withOperatorGroup opGroup1

            expr.SetProductions(
                ProductionBuilder(expr, "+", expr),
                ProductionBuilder(expr, "-", expr),
                ProductionBuilder(number)
            )

            expr
            |> DesigntimeFarkle.cast
            |> DesigntimeFarkle.withOperatorGroup opGroup2
            |> DesigntimeFarkleBuild.createGrammarDefinition
            |> DesigntimeFarkleBuild.buildGrammarOnly

        Expect.wantError grammar "Building had succeeded"
        |> Flip.Expect.all "Not all conflicts could not be resolved because of different operator groups" (function
        | BuildError.LALRConflict {Reason = DifferentOperatorGroup} -> true
        | _ -> false)
    }

    test "Reduce-Reduce conflicts are resolved only on demand" {
        let mkGrammar (resolveRRConflicts: bool) =
            let prec1 = obj()
            let prec2 = obj()

            let x1 = "X1" |||= [empty |> prec prec1]
            let x2 = "X2" |||= [empty |> prec prec2]

            let expr = "Expr" |||= [
                !& "x" .>> x1
                !& "x" .>> x2
            ]

            let opGroup =
                OperatorGroup(resolveRRConflicts,
                    NonAssociative(prec1),
                    NonAssociative(prec2)
                )

            expr
            |> DesigntimeFarkle.cast
            |> DesigntimeFarkle.autoWhitespace false
            |> DesigntimeFarkle.withOperatorGroup opGroup
            :> DesigntimeFarkle

        let grammarNotResolved =
            mkGrammar false
            |> DesigntimeFarkleBuild.createGrammarDefinition
            |> DesigntimeFarkleBuild.buildGrammarOnly
        Expect.wantError grammarNotResolved "Building had succeeded"
        |> Flip.Expect.all "Not all conflicts could not be resolved because of the operator group's inability" (function
        | BuildError.LALRConflict {Reason = CannotResolveReduceReduce; Type = ReduceReduce _} -> true
        | _ -> false)

        let runtimeResolved =
            mkGrammar true
            |> RuntimeFarkle.buildUntyped
            |> RuntimeFarkle.changePostProcessor PostProcessors.ast
        Expect.isTrue runtimeResolved.IsBuildSuccessful "Building failed"

        let expectedAST =
            let grammar = runtimeResolved.GetGrammar()
            let nont idx xs = AST.Nonterminal(grammar.Productions.[idx], xs)
            nont 1 [
                AST.Content(grammar.GetTerminalByName "x", Position.Create 1UL 1UL 0UL, "x")
                nont 3 []
            ]
        Expect.equal (runtimeResolved.Parse "x") (Ok expectedAST) "The parsed AST is different than the expected"
    }
]
