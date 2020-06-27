// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle.IO.CharStream
open Farkle.Tests

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, str, _)) ->
            Flip.Expect.isTrue "Unexpected end of input" <| cs.TryExpandPastOffset(0)
            Expect.equal cs.CharacterBuffer.[0] str.[0] "Character mismatch")

        // TODO: Write a better test for the character stream.
        ptestProperty "Consuming a character stream by a specified number of characters works as expected"
            (fun (CS(cs, str, steps)) ->
                use cs = cs
                let span = pinSpan cs (uint64 steps)
                advance false cs (span.IndexTo)
                Expect.equal steps (int <| cs.CurrentIndex) "An unexpected number of characters was consumed"
                let s = unpinSpanAndGenerateString cs span
                Expect.equal (str.Substring(0, steps)) s "The generated string is different from the original"
                Expect.throws (fun () -> unpinSpanAndGenerateString cs span |> ignore) "Generating a character span can be done more than once")
    ]
