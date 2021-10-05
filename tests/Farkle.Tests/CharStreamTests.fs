// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle
open Farkle.IO
open Farkle.Tests

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, str, _)) ->
            Flip.Expect.isTrue "Unexpected end of input" <| cs.TryExpandPastOffset(0)
            Expect.equal cs.CharacterBuffer.[0] str.[0] "Character mismatch")

        test "All kinds on line endings are correctly handled" {
            use cs = new CharStream("\n\r\n")
            Expect.equal cs.CurrentPosition Position.Initial
                "The character stream started at a different position"
            cs.AdvanceBy 1
            Expect.equal cs.CurrentPosition (Position.Create1 2 1)
                "The character stream is at a different position after LF"
            cs.AdvanceBy 2
            Expect.equal cs.CurrentPosition (Position.Create1 3 1)
                "The character stream is at a different position after CRLF"
        }
    ]
