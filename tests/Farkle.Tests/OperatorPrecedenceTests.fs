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
#if false // TODO-FARKLE7: Reevaluate when the samples are ported to Farkle 7.
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
#endif

    test "Reduce-Reduce conflicts are resolved only on demand" {
        let mkGrammar (resolveRRConflicts: bool) =
            let prec1 = obj()
            let prec2 = obj()

            let x1 = "X1" ||= [empty |> prec prec1 =% 1]
            let x2 = "X2" ||= [empty |> prec prec2 =% 2]

            let expr = "Expr" ||= [
                !& "x" .>>. x1 |> asProduction
                !& "x" .>>. x2 |> asProduction
            ]

            let opScope =
                OperatorScope(resolveRRConflicts,
                    NonAssociative(prec1),
                    NonAssociative(prec2)
                )

            expr
            |> _.AutoWhitespace(false)
            |> _.WithOperatorScope(opScope)

        let grammarNotResolved =
            mkGrammar false
            |> GrammarBuilder.build
        Expect.isTrue grammarNotResolved.IsFailing "Building should have failed"
        let lrStateMachine = grammarNotResolved.GetGrammar().LrStateMachine
        Expect.isNotNull lrStateMachine "The LR state machine was not built"
        Expect.isTrue lrStateMachine.HasConflicts "The LR state machine does not have conflicts"

        let runtimeResolved =
            mkGrammar true
            |> GrammarBuilder.build
        Expect.isFalse runtimeResolved.IsFailing "Building failed"
        Expect.equal (runtimeResolved.Parse "x") (ParserResult.CreateSuccess 2) "The resolved reduction is different than the expected"
    }

    test "Non-associative operators work" {
        let runtime =
            let number = Terminals.int "Number"
            let expr = nonterminal "Expr"
            expr.SetProductions(
                !@ expr .>> "+" .>>. expr => (+),
                !@ expr .>> "*" .>>. expr => ( * ),
                !@ number |> asProduction
            )

            // Both the string literal and the designtime Farkle literal should be equivalent.
            let opScope = OperatorScope(LeftAssociative("+"), NonAssociative(literal "*"))

            expr
            |> _.WithOperatorScope(opScope)
            |> GrammarBuilder.build

        Expect.equal (runtime.Parse "3+4+5") (ParserResult.CreateSuccess 12) "Parsing failed"
        expectIsParseFailure (runtime.Parse "3*4*5") "Parsing did not fail"
    }
]
