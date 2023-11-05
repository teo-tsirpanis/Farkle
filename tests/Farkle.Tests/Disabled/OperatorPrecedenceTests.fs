// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.OperatorPrecedenceTests

open Expecto
open Farkle
open Farkle.Samples.FSharp
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

            let opScope1 = OperatorScope(LeftAssociative("+"))
            let opScope2 = OperatorScope(LeftAssociative("-"))

            let number =
                Terminals.int "Number"
                |> DesigntimeFarkle.withOperatorScope opScope1

            expr.SetProductions(
                !% expr .>> "+" .>> expr,
                !% expr .>> "-" .>> expr,
                !% number
            )

            expr
            |> DesigntimeFarkle.withOperatorScope opScope2
            |> DesigntimeFarkleBuild.createGrammarDefinition
            |> DesigntimeFarkleBuild.buildGrammarOnly

        Expect.wantError grammar "Building had succeeded"
        |> Flip.Expect.all "Not all conflicts could not be resolved because of different operator scopes" (function
        | BuildError.LALRConflictReport _
        | BuildError.LALRConflict {Reason = DifferentOperatorScope} -> true
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

            let opScope =
                OperatorScope(resolveRRConflicts,
                    NonAssociative(prec1),
                    NonAssociative(prec2)
                )

            expr
            |> DesigntimeFarkle.autoWhitespace false
            |> DesigntimeFarkle.withOperatorScope opScope

        let grammarNotResolved =
            mkGrammar false
            |> DesigntimeFarkleBuild.createGrammarDefinition
            |> DesigntimeFarkleBuild.buildGrammarOnly
        Expect.wantError grammarNotResolved "Building had succeeded"
        |> Flip.Expect.all "Not all conflicts could not be resolved because of the operator scope's inability" (function
        | BuildError.LALRConflictReport _
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
                AST.Content(grammar.GetTerminalByName "x", Position.Initial, "x")
                nont 3 []
            ]
        Expect.equal (runtimeResolved.Parse "x") (Ok expectedAST) "The parsed AST is different than the expected"
    }

    test "Non-associative operators work" {
        let runtime =
            let number = Terminals.int "Number"
            let expr = nonterminal "Expr"
            expr.SetProductions(
                !@ expr .>> "+" .>>. expr => (+),
                !@ expr .>> "*" .>>. expr => ( * ),
                !@ number |> asIs
            )

            // Both the string literal and the designtime Farkle literal should be recognized as equals.
            let opScope = OperatorScope(LeftAssociative("+"), NonAssociative(literal "*"))

            expr
            |> DesigntimeFarkle.withOperatorScope opScope
            |> RuntimeFarkle.build

        Expect.equal (runtime.Parse "3+4+5") (Ok 12) "Parsing failed"
        Expect.isError (runtime.Parse "3*4*5") "Parsing did not fail"
    }
]
