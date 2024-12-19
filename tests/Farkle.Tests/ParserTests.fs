// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Diagnostics
open Farkle.Parser.Semantics
open Farkle.Samples
open Farkle.Samples.FSharp
open Farkle.Tests
open System.IO
open System.Text.Json.Nodes

/// A domain-ignorant Abstract Syntax Tree that describes the output of a parser.
/// Used to be part of Farkle 6's public API but was removed in Farkle 7.
[<RequireQualifiedAccess>]
type AST =
    | Content of int * TextPosition * string
    | Nonterminal of int * AST list

let astSemanticProvider = {new ISemanticProvider<char, AST> with
    member _.Transform(state, symbol, chars) = AST.Content(symbol.Value, state.CurrentPosition,chars.ToString())
    member _.Fuse(_, production, members) =
        let mutable membersList = []
        for i = members.Length - 1 downto 0 do
            membersList <- members[i] :?> AST :: membersList
        AST.Nonterminal(production.Value, membersList)
}

let testParser grammarFile displayName text =
    let testImpl streamMode useStaticBlock =
        let description = $"Grammar \"{grammarFile}\" parses %s{displayName} successfully in {streamMode} block mode"
        test description {
            let rf = loadCharParser grammarFile
            let result =
                if useStaticBlock then
                    CharParser.parseString rf text
                else
                    use sr = new StringReader(text)
                    CharParser.parseTextReader rf sr
                |> ParserResult.toResult
            Expect.isOk result "Parsing failed"
        }
    [
        testImpl "static" true
        testImpl "dynamic" false
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
            let nont idx xs = AST.Nonterminal(idx, xs)
            let numberTerminal = 6
            let makePos line col = TextPosition.Create1(line, col)
            nont 0 [
                nont 6 [
                    AST.Content(numberTerminal, makePos 1 1, "475")
                ]
                AST.Content(0, makePos 1 5, "+")
                nont 6 [
                    AST.Content(numberTerminal, makePos 1 7, "724")
                ]
            ]

        let num =
            CharParser.parseString SimpleMaths.int testString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Parsing failed"
        let astParser = CharParser.withSemanticProvider astSemanticProvider SimpleMaths.int
        let ast =
            CharParser.parseString astParser testString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Parsing failed"
        Expect.equal num 1199 "The numerical result is different than the expected"
        Expect.equal ast expectedAST "The AST is different"
    }

    test "Lexical errors report the correct position" {
        let jsonString = """{"Almost True": truffle}"""
        let error = expectWantParseFailure (JSON.parser.Parse jsonString) "Parsing should have failed"
        match error with
        // Prior to Farkle 7, the error was reported at 1:20, the place where the unrecognized character was found.
        // Now, the error is reported at 1:17, the place where the tokenizer last started.
        | ParserDiagnostic(TextPosition(1, 17), LexicalError(ValueSome "tru")) -> ()
        | error -> failtest $"Unexpected parser error {error}"
    }

    test "Syntax errors report the correct position" {
        let jsonString = """{"Almost True": }"""
        let error = expectWantParseFailure (JSON.parser.Parse jsonString) "Parsing should have failed"
        match error with
        | ParserDiagnostic(TextPosition(1, 17), SyntaxError(_, ValueSome "}")) -> ()
        | error -> failtest $"Unexpected parser error {error}"
    }

    test "Parsing a mathematical expression with comments works well" {
        let num = CharParser.parseString SimpleMaths.int "/*I guess that */ 1 + 1\n// Is equal to two"
        Expect.equal num (ParserResult.CreateSuccess 2) "Parsing a mathematical expression with comments failed"
    }

    testProperty "The JSON parser works well" (fun json ->
        let formatJson (json: JsonNode) = match json with null -> "null" | _ -> json.ToJsonString()

        let jsonAsString = formatJson json
        let farkleFSharp =
            CharParser.parseString JSON.parser jsonAsString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Farkle's parser failed"
            |> formatJson
        Expect.equal farkleFSharp jsonAsString "The JSON generated from the Farkle F# parser is different"
        let farkleCSharp =
            CharParser.parseString CSharp.JSON.Parser jsonAsString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Farkle's parser failed"
            |> formatJson
        Expect.equal farkleCSharp jsonAsString "The JSON generated from the Farkle C# parser is different"
    )

    testProperty "The calculator works well" (fun expr ->
        let exprAsString = SimpleMaths.renderExpression expr
        let parsedExpr =
            CharParser.parseString SimpleMaths.mathExpression exprAsString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Parsing the mathematical expression failed"
        let num =
            CharParser.parseString SimpleMaths.int exprAsString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Calculating the mathematical expression failed"
        Expect.equal num parsedExpr.Value "The directly calculated value of the expression differs from the parsed one"
    )

    test "The Farkle-built grammar that recognizes the GOLD Meta-Language works well" {
        let result = GOLDMetaLanguage.parser.Parse gmlSourceContent |> ParserResult.toResult
        Expect.isOk result "Parsing the GOLD Meta-Language file describing itself failed"
    }

    test "Windows line endings inside block groups are correctly handled" {
        let rf =
            literal "hello"
            |> _.AddBlockComment("/*", "*/")
            |> GrammarBuilder.buildSyntaxCheck
        let testString = "/*\r\n\r\n\r\n*/ hell"
        let error = expectWantParseFailure (rf.Parse testString) "Parsing should have failed"
        match error with
        | ParserDiagnostic(TextPosition(4, 4), LexicalError(ValueSome "hell")) -> ()
        | _ -> failtest $"Unexpected parser error {error}"
    }
]
