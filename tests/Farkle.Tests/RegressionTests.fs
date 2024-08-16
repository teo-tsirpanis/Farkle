// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegressionTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Diagnostics

let private reproduceIssue issueNumber = test (sprintf "GitHub issue #%02i" issueNumber)

let private freproduceIssue issueNumber = ftest (sprintf "GitHub issue #%02i" issueNumber)

[<Tests>]
let tests = testList "Regression tests" [
    reproduceIssue 8 {
        let parser = loadCharParser "issue-8.egt"
        let result = CharParser.parseString parser "45"
        Expect.equal result (ParserResult.CreateSuccess()) "The two-digit input was not successfully parsed"

        let (|TextPosition|) (pos: TextPosition) = TextPosition(pos.Line, pos.Column)
        let result = CharParser.parseString parser "3"
        match result with
        | ParserError(ParserDiagnostic(TextPosition(1, 1), LexicalError _)) -> ()
        | _ -> failtestNoStackf $"The issue was not reproduced: {result}"
    }

    reproduceIssue 279 {
        let grammar =
            let term1 = virtualTerminal "T1"
            let term2 = virtualTerminal "T2"

            let nont1 = nonterminalU "N1"
            let nont2 = nonterminalU "N2"

            nont1.SetProductions(
                !% term1,
                !% term2 .>> nont2 .>> term2
            )
            nont2.SetProductions(
                !% nont1 .>> nont2,
                !% nont1,
                empty
            )
            nont1
            |> GrammarBuilder.buildSyntaxCheck
            |> _.GetGrammar()

        Expect.isTrue grammar.LrStateMachine.HasConflicts "The grammar should have conflicts"
    }
]
