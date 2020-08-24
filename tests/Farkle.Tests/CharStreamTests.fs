// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle.IO
open Farkle.Tests

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, str, _)) ->
            Flip.Expect.isTrue "Unexpected end of input" <| cs.TryExpandPastOffset(0)
            Expect.equal cs.CharacterBuffer.[0] str.[0] "Character mismatch")

        // TODO: Write a better test for the character stream.
    ]
