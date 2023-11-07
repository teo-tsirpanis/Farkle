// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Farkle
open Farkle.Diagnostics
open Farkle.Parser.Semantics
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
            membersList <- members.[i] :?> AST :: membersList
        AST.Nonterminal(production.Value, membersList)
}

let testParser grammarFile displayName text =
    let testImpl streamMode useStaticBlock =
        let description = sprintf "Grammar \"%s\" parses %s successfully in %s block mode" grammarFile displayName streamMode
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

#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
    test "Parsing a simple mathematical expression generates the correct AST" {
        let testString = "475 + 724"
        let expectedAST =
            let nont idx xs = AST.Nonterminal(idx, xs)
            let numberTerminal = 7
            let makePos line col = TextPosition.Create1(line, col)
            nont 0 [
                nont 6 [
                    AST.Content(numberTerminal, makePos 1 1, "475")
                ]
                AST.Content(6, makePos 1 5, "+")
                nont 6 [
                    AST.Content(numberTerminal, makePos 1 7, "724")
                ]
            ]

        let num =
            CharParser.parseString SampleParsers.simpleMaths testString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Parsing failed"
        let astParser = CharParser.withSemanticProvider astSemanticProvider SampleParsers.simpleMaths
        let ast =
            CharParser.parseString astParser testString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Parsing failed"
        Expect.equal num 1199 "The numerical result is different than the expected"
        Expect.equal ast expectedAST "The AST is different"
    }
#endif

    test "Lexical errors report the correct position" {
        let jsonString = """{"Almost True": truffle}"""
        let result = CharParser.parseString SampleParsers.json jsonString
        Expect.isTrue result.IsError "Parsing did not fail"
        match result.Error with
        | ParserDiagnostic(pos, _) ->
            // Prior to Farkle 7, the error was reported at 1:20, the place where the unrecognized character was found.
            // Now, the error is reported at 1:17, the place where the tokenizer last started.
            Expect.equal pos (TextPosition.Create1(1, 17)) "The position is different than the expected"
        | _ -> failtest "The error is not a ParserDiagnostic"
    }

    test "Parsing a mathematical expression with comments works well" {
        let num = CharParser.parseString SampleParsers.simpleMaths "/*I guess that */ 1 + 1\n// Is equal to two"
        Expect.equal num (ParserResult.CreateSuccess 2) "Parsing a mathematical expression with comments failed"
    }

    testProperty "The JSON parser works well" (fun json ->
        let formatJson (json: JsonNode) = match json with null -> "null" | _ -> json.ToJsonString()

        let jsonAsString = formatJson json
#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
        let farkleFSharp =
            RuntimeFarkle.parseString FSharp.JSON.runtime jsonAsString
            |> Flip.Expect.wantOk "Farkle's parser failed"
            |> formatJson
        Expect.equal farkleFSharp jsonAsString "The JSON generated from the Farkle F# parser is different"
        let farkleCSharp =
            RuntimeFarkle.parseString CSharp.JSON.Runtime jsonAsString
            |> Flip.Expect.wantOk "Farkle's parser failed"
            |> formatJson
        Expect.equal farkleCSharp jsonAsString "The JSON generated from the Farkle C# parser is different"
#else
        let farkle =
            CharParser.parseString SampleParsers.json jsonAsString
            |> ParserResult.toResult
            |> Flip.Expect.wantOk "Farkle's parser failed"
            |> formatJson
        Expect.equal farkle jsonAsString "The JSON generated from the Farkle parser is different"
#endif
    )

#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
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
#endif
]
