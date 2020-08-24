// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle.IO
open Farkle.IO.CharStream
open Farkle.Tests

let private toStringTransformer =
    {new ITransformer<unit> with member _.Transform(_, _, data) = box <| data.ToString()}

/// Creates a string out of the characters at the given `CharSpan`.
/// After that call, the characters at and before the span might be
/// freed from memory, so this function must not be used twice.
/// It is recommended to use the `unpinSpanAndGenerate` function
/// to avoid excessive allocations, unless you specifically want a string.
[<CompiledName("UnpinSpanAndGenerateString")>]
let private unpinSpanAndGenerateString cs =
    let s =
        unpinSpanAndGenerateObject
            ()
            toStringTransformer
            cs
    s :?> string

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, str, _)) ->
            Flip.Expect.isTrue "Unexpected end of input" <| cs.TryExpandPastOffset(0)
            Expect.equal cs.CharacterBuffer.[0] str.[0] "Character mismatch")

        // TODO: Write a better test for the character stream.
        // ptestProperty "Consuming a character stream by a specified number of characters works as expected"
        //     (fun (CS(cs, str, steps)) ->
        //         use cs = cs
        //         let span = pinSpan cs (uint64 steps)
        //         advance false cs (span.IndexTo)
        //         Expect.equal steps (int <| cs.CurrentIndex) "An unexpected number of characters was consumed"
        //         let s = unpinSpanAndGenerateString cs span
        //         Expect.equal (str.Substring(0, steps)) s "The generated string is different from the original"
        //         Expect.throws (fun () -> unpinSpanAndGenerateString cs span |> ignore) "Generating a character span can be done more than once")
    ]
