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
            Flip.Expect.isTrue "Unexpected end of input" <| cs.TryLoadFirstCharacter()
            Expect.equal cs.FirstCharacter str.[0] "Character mismatch")

        testProperty "Consuming a character stream by a specified number of characters works as expected"
            (fun (CS(cs, str, steps)) ->
                use cs = cs
                let idx =
                    let rec impl idx n =
                        let mutable c = '\u0549'
                        match readChar cs idx &c with
                        | true when n = steps -> idx
                        | true -> impl (idx + 1UL) (n + 1)
                        | false -> failtestf "Unexpected end of file after %d iterations" n
                    impl cs.CurrentIndex 1
                let span = pinSpan cs idx
                advance false cs idx
                Expect.equal steps (int <| cs.CurrentIndex) "An unexpected number of characters was consumed"
                let s = unpinSpanAndGenerateString cs span
                Expect.equal (str.Substring(0, steps)) s "The generated string is different from the original"
                Expect.throws (fun () -> unpinSpanAndGenerateString cs span |> ignore) "Generating a character span can be done more than once")
    ]
