// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle.Collections.CharStream
open Farkle.Tests
open FsCheck

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, _)) ->
            let c = cs.FirstCharacter
            let mutable c2 = '\u0640'
            let mutable idx = getCurrentIndex cs
            readChar cs &c2 &idx && c = c2)

        testProperty "Consuming the a character stream by a specified number of characters works as expected"
            (fun (CS(cs, str)) steps -> (steps < str.Length && steps <> 0) ==> (fun () ->
                let mutable idx = getCurrentIndex cs
                let mutable c = '\u0549'
                for n = 1 to steps - 1 do
                    if not <| readChar cs &c &idx then
                        failtestf "Unexpected end of file after %d iterations" n
                let span = pinSpan cs idx
                consume false cs span
                Expect.equal steps (int cs.Position.Index) "An unexpected number of characters was consumed"
                let s = unpinSpanAndGenerateString cs span |> fst
                Expect.equal steps s.Length "The generated string had an unexpected end"
                Expect.throws (fun () -> unpinSpanAndGenerateString cs span |> ignore) "Generating a character span can be done more than once"))
    ]
