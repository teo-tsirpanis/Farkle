// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarTests

open Expecto
open Farkle.Grammar
open Farkle.Grammar.GOLDParser

[<Tests>]
let tests =
    testList "Grammar tests" [
        test "A legacy CGT grammar fails to be read." {
            let x = loadGrammar "legacy.cgt"
            Expect.equal x (Error ReadACGTFile) "Reading the legacy grammar did not fail"
        }

        test "An EGT file is successfuly read" {
            let x = loadGrammar "simple.egt"
            Expect.isOk x "Reading the grammar failed"
        }

        test "An invalid Base64-encoded grammar string does not throw an exception" {
            let x = EGT.ofBase64String "👏🏻👏🏻👏🏻👏🏻👏🏻"
            Expect.equal x (Error InvalidBase64Format) "Reading the invalid Base64 string did not fail"
        }

        test "Terminal naming works properly" {
            let fTerminal (term, expected) =
                (1u, term)
                |> Terminal
                |> string
                |> Flip.Expect.equal "String value of a terminal is not the same as expected." expected
            [
                "'", "''"
                ".NET", "'.NET'"
                ".NET Core", "'.NET Core'"
                "Number", "Number"
                "*", "'*'"
                "starts.with_a-letter", "starts.with_a-letter"
                "#134<=>#642", "'#134<=>#642'"
            ] |> List.iter fTerminal
        }

        test "Terminals with the same index are equal" {
            let t1 = Terminal(0u, "Terminal 1")
            let t2 = Terminal(1u, "Terminal 2")
            let t3 = Terminal(0u, "Terminal 3")

            Expect.isTrue (t1 = t3) "Terminals with the same index are not equal"
            Expect.isFalse (t2 = t3) "Terminals with different indices are equal"
        }

        test "Nonterminals with the same index are equal" {
            let n1 = Nonterminal(0u, "Nonterminal 1")
            let n2 = Nonterminal(1u, "Nonterminal 2")
            let n3 = Nonterminal(0u, "Nonterminal 3")

            Expect.isTrue (n1 = n3) "Nonterminals with the same index are not equal"
            Expect.isFalse (n2 = n3) "Nonterminals with different indices are equal"
        }
    ]
