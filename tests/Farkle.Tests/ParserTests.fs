// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.ParserTests

open Expecto
open Expecto.Logging
open Farkle
open Farkle.Collections
open Farkle.PostProcessor
open System.IO

let logger = Log.create "Parser tests"

let testParser grammarFile displayName text =
    let testImpl streamMode fCharStream =
        let description = sprintf "Grammar \"%s\" parses %s successfully in %s block mode." grammarFile displayName streamMode
        test description {
            let rf = RuntimeFarkle.createFromPostProcessor PostProcessor.ast grammarFile
            let result = RuntimeFarkle.parseChars rf (string >> Message.eventX >> logger.debug) |> using (fCharStream text)
            match result with
            | Ok _ -> ()
            | Result.Error x -> failtest <| x.ToString()
        }
    [
        testImpl "static" CharStream.ofString
        testImpl "dynamic" (fun x -> new StringReader(x) |> CharStream.ofTextReader)
    ]

[<Tests>]
let tests =
    [
        "simple.egt", "\"111*555\"", "111 * 555"
        "inception.egt", "its own definition file", File.ReadAllText "inception.grm"
    ]
    |> List.collect ((<|||) testParser)
    |> testList "Parser tests"
