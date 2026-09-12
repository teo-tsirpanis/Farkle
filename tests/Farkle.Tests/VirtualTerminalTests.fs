// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.VirtualTerminalTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Diagnostics
open Farkle.Grammars
open Farkle.Parser
open Farkle.Samples.FSharp.IndentBased

[<Tests>]
let tests = testList "Virtual terminal tests" [
    test "A simple IndentCode file is successfully parsed" {
        let code = Block [Line "USS Oriskany"; Block [Line "CV"; Line "34"]]
        let rendered = "
USS Oriskany

    CV

    34
    "

        ["static"; "dynamic"]
        |> List.iter (fun mode ->
            let result = if mode = "static" then parser.Parse rendered else parseGradual 1 parser rendered
            let result = expectWantParseSuccess result $"Parsing in {mode} block mode failed"

            Expect.equal result code "The parsed IndentCode is different from the original"
        )
    }

    test "An IndentCode file with invalid indentation fails to be parsed" {
        let code = "A\n    B\n   C"
        let result = expectWantParseFailure (parser.Parse code) "Parsing should have failed"
        match result with
        | ParserDiagnostic(TextPosition(3, 4), msg) when msg = "unindent does not match any outer indentation level" -> ()
        | _ -> failtest $"Unexpected parser error {result}"
    }

    test "A grammar with only virtual terminals can be built" {
        let grammar =
            virtualTerminal "X"
            |> _.AutoWhitespace(false)
            |> _.BuildSyntaxCheck()
            |> _.GetGrammar()

        Expect.isNotNull grammar.DfaOnChar "The grammar does not have a DFA"
        Expect.hasLength grammar.DfaOnChar 1 "The DFA does not have the expected number of states"
        Expect.isEmpty grammar.DfaOnChar.[0].Edges "The DFA should not have edges"
    }

    test "The transformer of a typed virtual terminal is invoked when it must be" {
        // No need to spin up a full parser with a custom tokenizer for this.
        let mutable invoked = false
        let output =
            virtualTerminalT "X" (T(fun _ _ -> invoked <- true; 42))
            |> _.AddSpecialName("X")
            |> _.AutoWhitespace(false)
            |> _.Build(BuilderOutputs.GrammarSummary ||| BuilderOutputs.SemanticProviderOnChar)
        let handle = output.Grammar.GetTokenSymbolFromSpecialName "X"
        let mutable parserState = ParserState()
        let semanticValue = output.SemanticProviderOnChar.Transform(&parserState, handle, "")
        Expect.isTrue invoked "The transformer was not invoked"
        Expect.equal semanticValue 42 "The returned semantic value is different from the expected"
    }
]
