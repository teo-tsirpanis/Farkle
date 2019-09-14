// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Grammar
open Farkle.IO
open Farkle.JSON
open Farkle.Parser
open Farkle.Tests
open System.IO

let logger = Log.create "Parser tests"

let testParser grammarFile displayName text =
    let testImpl streamMode fCharStream =
        let description = sprintf "Grammar \"%s\" parses %s successfully in %s block mode." grammarFile displayName streamMode
        test description {
            let rf = loadRuntimeFarkle grammarFile
            let result = RuntimeFarkle.parseChars rf (string >> Message.eventX >> logger.verbose) |> using (fCharStream text)
            Expect.isOk result "Parsing failed"
        }
    [
        testImpl "static" CharStream.ofString
        testImpl "dynamic" (fun x -> new StringReader(x) |> CharStream.ofTextReader)
    ]

[<Tests>]
let tests = testList "Parser tests" [
    [
        "simple.egt", "\"111*555\"", "111 * 555"
        "gml.egt", "its own definition file", File.ReadAllText <| getResourceFile "gml.grm"
    ]
    |> List.collect ((<|||) testParser)
    |> testList "Domain-ignorant parsing tests"

    test "Parsing a simple mathematical expression behaves correctly and consistently up to the parsing log" {
        let grammar = SimpleMaths.int.TryGetGrammar() |> returnOrFail "%O"
        let reduce idx = ParseMessage.Reduction grammar.Productions.[int idx]
        let numberTerminal = Terminal(3u, "Number")

        let expectedLog = [
            ParseMessage.TokenRead {Symbol = numberTerminal; Position = Position.Create 1UL 1UL 0UL; Data = 475}
            ParseMessage.Shift 9u
            ParseMessage.TokenRead {Symbol = Terminal(0u, "+"); Position = Position.Create 1UL 5UL 4UL; Data = null}
            reduce 0u
            reduce 3u
            reduce 6u
            reduce 9u
            ParseMessage.Shift 3u
            ParseMessage.TokenRead {Symbol = numberTerminal; Position = Position.Create 1UL 7UL 6UL; Data = 724}
            ParseMessage.Shift 9u
            ParseMessage.EndOfInput <| Position.Create 1UL 10UL 9UL
            reduce 0u
            reduce 3u
            reduce 6u
            reduce 7u
            reduce 10u
        ]

        let actualLog = ResizeArray()
        let num = RuntimeFarkle.parseString SimpleMaths.int actualLog.Add "475 + 724" |> returnOrFail "Parsing '475 + 724' failed: %O"
        Expect.equal num 1199 "The numerical result is different than the expected"
        Expect.sequenceEqual actualLog expectedLog "The parsing log is different than the usual"
    }

    test "Parsing a mathematical expression with comments works well" {
        let num = RuntimeFarkle.parse SimpleMaths.int "/*I guess that */ 1 + 1\n// Is equal to two"
        Expect.equal num (Ok 2) "Parsing a mathematical expression with comments failed"
    }

    testProperty "The JSON parser works well" (fun json ->
        let jsonAsString = Chiron.Formatting.Json.format json
        let cs =
            RuntimeFarkle.parse CSharp.Language.Runtime jsonAsString
            |> returnOrFail "The C# parser failed: %O"
        let fs =
            RuntimeFarkle.parse FSharp.Language.runtime jsonAsString
            |> returnOrFail "The F# parser failed: %O"
        let chiron =
            match Chiron.Parsing.Json.tryParse jsonAsString with
            | Choice1Of2 x -> x
            | Choice2Of2 x -> failtestf "The Chiron parser failed: %s" x
        Expect.equal cs json "The JSON structure generated from the C# Farkle parser is different"
        Expect.equal fs json "The JSON structure generated from the F# Farkle parser is different"
        Expect.equal chiron json "The JSON structure generated from the Chiron parser is different"
    )

    testProperty "The calculator works well" (fun expr ->
        let exprAsString = SimpleMaths.renderExpression expr
        let parsedExpr =
            RuntimeFarkle.parse SimpleMaths.mathExpression exprAsString
            |> returnOrFail "Parsing the mathematical expression failed: %O"
        let num =
            RuntimeFarkle.parse SimpleMaths.int exprAsString
            |> returnOrFail "Calculating the mathematical expression failed: %O"
        Expect.equal num parsedExpr.Value "The directly calculated value of the expression differs from the parsed one."
    )
]
