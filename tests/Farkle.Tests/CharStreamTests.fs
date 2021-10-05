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

        test "Position tracking works when all characters are advanced at once" {
            use cs = new CharStream("\r \n\r\n")
            cs.AdvanceBy cs.CharacterBuffer.Length
            Expect.equal cs.CurrentPosition (Position.Create1 4 1)
                "The position at the end of the characters is different"
        }

        test "Position tracking works when each character is advanced at a time" {
            use cs = new CharStream("\r \n\r\n")
            Expect.equal cs.CurrentPosition Position.Initial
                "The character stream started at a different position"
            let expectedPositions = [
                // The position tracker initially ignores the first CR.
                Position.Initial
                // Only when it sees that the next character is not an LF
                // it decides to move to the next line.
                Position.Create1 2 2
                // It now sees an LF. Easy choice.
                Position.Create1 3 1
                // Another CR; ignore it at first, but remember we last saw a CR.
                Position.Create1 3 1
                // An LF right after a CR. The position tracker changes the line only once.
                Position.Create1 4 1
            ]
            let actualPositions = cs |> List.unfold (fun cs ->
                if cs.CharacterBuffer.IsEmpty then
                    None
                else
                    cs.AdvanceBy 1
                    Some (cs.CurrentPosition, cs))
            Expect.sequenceEqual actualPositions expectedPositions
                "The character stream's positions are different"
        }
    ]
