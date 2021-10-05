// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammar
open Farkle.IO
open Farkle.Samples
open Farkle.Parser
open Farkle.Tests
open System.IO

module SimpleMaths = Farkle.Samples.FSharp.SimpleMaths

let testParser grammarFile displayName text =
    let testImpl streamMode fCharStream =
        let description = sprintf "Grammar \"%s\" parses %s successfully in %s block mode" grammarFile displayName streamMode
        test description {
            let rf = loadRuntimeFarkle grammarFile
            let result = RuntimeFarkle.parseChars rf |> using (fCharStream text)
            Expect.isOk result "Parsing failed"
        }
    [
        testImpl "static" (CharStream: string -> _)
        testImpl "dynamic" (fun x -> StringReader(x) |> CharStream)
    ]

let gmlSourceContent = File.ReadAllText <| getResourceFile "gml.grm"

[<Tests>]
let tests = testList "Parser tests" [
    [
        "simple.egt", "\"111*555\"", "111 * 555"
        "gml.egt", "its own definition file", gmlSourceContent
    ]
    |> List.collect ((<|||) testParser)
    |> testList "Domain-ignorant parsing tests"

    test "Parsing a simple mathematical expression generates the correct AST" {
        let testString = "475 + 724"
        let expectedAST =
            let grammar = SimpleMaths.int.GetGrammar()
            let nont idx xs = AST.Nonterminal(grammar.Productions.[idx], xs)
            let numberTerminal = Terminal(3u, "Number")
            nont 0 [
                nont 6 [
                    AST.Content(numberTerminal, Position.Create1 1 1, "475")
                ]
                AST.Content(Terminal(0u, "+"), Position.Create1 1 5, "+")
                nont 6 [
                    AST.Content(numberTerminal, Position.Create1 1 7, "724")
                ]
            ]

        let num =
            RuntimeFarkle.parseString SimpleMaths.int testString
            |> Flip.Expect.wantOk "Parsing failed"
        let astRuntime = RuntimeFarkle.changePostProcessor PostProcessors.ast SimpleMaths.int
        let ast =
            RuntimeFarkle.parseString astRuntime testString
            |> Flip.Expect.wantOk "Parsing failed"
        Expect.equal num 1199 "The numerical result is different than the expected"
        Expect.equal (AST.toASCIITree ast) (AST.toASCIITree expectedAST) "The AST is different"
    }

    test "Lexical errors report the correct position" {
        let jsonString = "{\"Almost True\": truffle}"
        let result = RuntimeFarkle.parseString FSharp.JSON.runtime jsonString
        let error =
            ParserError(Position.Create1 1 20, ParseErrorType.LexicalError 'f')
            |> FarkleError.ParseError
            |> Result.Error
        Expect.equal result error "The wrong position was reported on a lexical error"
    }

    test "Parsing a mathematical expression with comments works well" {
        let num = RuntimeFarkle.parseString SimpleMaths.int "/*I guess that */ 1 + 1\n// Is equal to two"
        Expect.equal num (Ok 2) "Parsing a mathematical expression with comments failed"
    }

    testProperty "The JSON parser works well" (fun json ->
        let jsonAsString = Chiron.Formatting.Json.format json
        let farkle =
            RuntimeFarkle.parseString FSharp.JSON.runtime jsonAsString
            |> Flip.Expect.wantOk "Farkle's parser failed"
        let chiron =
            match Chiron.Parsing.Json.tryParse jsonAsString with
            | Choice1Of2 x -> x
            | Choice2Of2 x -> failtestf "The Chiron parser failed: %s" x
        Expect.equal farkle json "The JSON structure generated from the Farkle parser is different"
        Expect.equal chiron json "The JSON structure generated from the Chiron parser is different"
    )

    testProperty "The calculator works well" (fun expr ->
        let exprAsString = SimpleMaths.renderExpression expr
        let parsedExpr =
            RuntimeFarkle.parseString SimpleMaths.mathExpression exprAsString
            |> Flip.Expect.wantOk "Parsing the mathematical expression failed"
        let num =
            RuntimeFarkle.parseString SimpleMaths.int exprAsString
            |> Flip.Expect.wantOk "Calculating the mathematical expression failed"
        Expect.equal num parsedExpr.Value "The directly calculated value of the expression differs from the parsed one"
    )

    test "The Farkle-built grammar that recognizes the GOLD Meta-Language works well" {
        let result = FSharp.GOLDMetaLanguage.runtime.Parse gmlSourceContent
        Expect.isOk result "Parsing the GOLD Meta-Language file describing itself failed"
    }

    test "Windows line endings inside block groups are correctly handled" {
        let rf =
            literal "hello"
            |> DesigntimeFarkle.addBlockComment "/*" "*/"
            |> RuntimeFarkle.buildUntyped
        let testString = "/*\r\n\r\n\r\n*/ hell"
        let expectedResult =
            ParserError(Position.Create1 4 8, ParseErrorType.UnexpectedEndOfInput)
            |> FarkleError.ParseError
            |> Error
        let actualResult = rf.Parse testString
        Expect.equal actualResult expectedResult "Unexpected result"
    }
]
