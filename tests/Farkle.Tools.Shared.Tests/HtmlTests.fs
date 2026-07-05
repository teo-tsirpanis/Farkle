// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

module Farkle.Tools.Shared.Tests.HtmlTests

open Expecto
open Farkle.Builder
open Farkle.Grammars
open Farkle.Tools.Templating
open HtmlAgilityPack
open Serilog.Core
open System
open System.IO

let renderHtml grammar =
    GrammarHtml(GrammarTemplateInput.Create grammar "", HtmlOptions.Default)
    |> TemplateEngine.renderTemplate Logger.None
    |> Flip.Expect.wantOk "Rendering HTML failed"
    |> _.Content

[<Tests>]
let tests = testList "HTML tests" [
    Directory.EnumerateFiles(AppContext.BaseDirectory, "*.grammar.dat")
    |> Seq.map (fun file -> test (Path.GetFileName file) {
        let grammar = Grammar.ofFile file
        let rendered = renderHtml grammar
        let doc = HtmlDocument()
        doc.LoadHtml(rendered)
        Expect.isEmpty doc.ParseErrors "Document has parse errors"

        let assertHasIds prefix n =
            for n = 0 to n - 1 do
                let node = doc.GetElementbyId($"%s{prefix}{n}")
                Expect.isNotNull node $"Element with ID {prefix}{n} not found"
        assertHasIds "n" grammar.Nonterminals.Count
        assertHasIds "prod" grammar.Productions.Count
        match grammar.LrStateMachine with
        | null -> ()
        | lalr -> assertHasIds "lalr" lalr.Count
        match grammar.DfaOnChar with
        | null -> ()
        | dfa -> assertHasIds "dfa" dfa.Count
    })
    |> List.ofSeq
    |> testList "Rendering HTML documents of a grammar has all expected elements"

    test "A grammar with no state machines can be rendered" {
        let grammar =
            literal "hello"
            |> _.BuildSyntaxCheck(BuilderOutputs.GrammarSummary)
            |> _.Grammar
            |> nonNull
        let rendered = renderHtml grammar
        let doc = HtmlDocument()
        doc.LoadHtml(rendered)
        Expect.isEmpty doc.ParseErrors "Document has parse errors"
    }

    test "A grammar with conflicts can be rendered" {
        let grammar =
            let expr = nonterminalU "Expr"
            setProductionsU expr [
                !% expr .>> "+" .>> expr
                !% expr .>> "-" .>> expr
            ]
            expr
            |> _.BuildSyntaxCheck(BuilderOutputs.GrammarLrStateMachine)
            |> _.Grammar
            |> nonNull
        Expect.isTrue grammar.LrStateMachine.HasConflicts "Grammar should have conflicts"
        let rendered = renderHtml grammar
        let doc = HtmlDocument()
        doc.LoadHtml(rendered)
        Expect.isEmpty doc.ParseErrors "Document has parse errors"
    }
]
