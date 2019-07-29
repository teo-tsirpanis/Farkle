// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Collections
open Farkle.JSON
open Farkle.PostProcessor
open Farkle.Tests
open System.IO

let logger = Log.create "Parser tests"

let testParser grammarFile displayName text =
    let testImpl streamMode fCharStream =
        let description = sprintf "Grammar \"%s\" parses %s successfully in %s block mode." grammarFile displayName streamMode
        test description {
            let rf = RuntimeFarkle.ofEGTFile PostProcessor.ast (sprintf "../resources/%s" grammarFile)
            let result = RuntimeFarkle.parseChars rf (string >> Message.eventX >> logger.verbose) |> using (fCharStream text)
            match result with
            | Ok _ -> ()
            | Result.Error x -> failtest <| x.ToString()
        }
    [
        testImpl "static" CharStream.ofString
        testImpl "dynamic" (fun x -> new StringReader(x) |> CharStream.ofTextReader)
    ]

[<Tests>]
let tests = testList "Parser tests" [
    [
        "simple.egt", "\"111*555\"", "111 * 555"
        "gml.egt", "its own definition file", File.ReadAllText "../resources/gml.grm"
    ]
    |> List.collect ((<|||) testParser)
    |> testList "Domain-ignorant parsing tests"

    testProperty "The JSON parser works well" (fun json ->
        let jsonAsString = Chiron.Formatting.Json.format json
        let parsedFromFSharp = RuntimeFarkle.parse FSharp.Language.runtime jsonAsString
        let parsedFromCSharp = RuntimeFarkle.parse CSharp.Language.Runtime jsonAsString
        let parsedFromChiron = Chiron.Parsing.Json.tryParse jsonAsString
        match parsedFromFSharp, parsedFromCSharp, parsedFromChiron with
        | Ok cs, Ok fs, Choice1Of2 chiron ->
            Expect.equal cs json "The JSON structure generated from the C# Farkle parser is different"
            Expect.equal fs json "The JSON structure generated from the F# Farkle parser is different"
            Expect.equal chiron json "The JSON structure generated from the Chiron parser is different"
        | Result.Error x, _, _ ->
            failtestf "The C# parser failed: %O" x
        | _, Result.Error x, _ ->
            failtestf "The F# parser failed: %O" x
        | _, _, Choice2Of2 x ->
            failtestf "The Chiron parser failed: %O" x
        true
    )

    testProperty "The calculator works well" (fun expr ->
        let exprAsString = SimpleMaths.renderExpression expr
        let parsedExpression = RuntimeFarkle.parse SimpleMaths.mathExpression exprAsString
        let parsedNumber = RuntimeFarkle.parse SimpleMaths.int exprAsString
        match parsedExpression, parsedNumber with
        | Ok expr, Ok num ->
            Expect.equal num (SimpleMaths.evalExpression expr) "The directly calculated value of the expression differs from the parsed one."
        | Result.Error x, _ ->
            failtestf "Parsing the mathematical expression failed: %O" x
        | _, Result.Error x ->
            failtestf "Calculating the mathematical expression failed: %O" x
    )
]
