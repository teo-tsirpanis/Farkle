// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Expecto.Logging
open Farkle.Collections
open Farkle.Collections.CharStream
open Farkle.Tests
open FsCheck
open System

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, _)) ->
            let c = cs.FirstCharacter
            let v = CharStream.view cs
            match v with
            | CSCons(c2, _) -> c = c2
            | CSNil -> false)

        test "Character Stream Taking the first five characters of \"Hello World\" works as expected" {
            let cs = CharStream.ofReadOnlyMemory <| "Hello World".AsMemory()
            let v = CharStream.view cs
            match v with
            | CSCons('H', CSCons('e', CSCons('l', CSCons('l', CSCons('o', vs))))) ->
                let span = pinSpan vs
                let s = unpinSpanAndGenerate null (CharStreamCallback (fun _ _ x -> box <| x.ToString())) cs span |> fst :?> _
                Expect.equal "Hello" s "First five characters of \"Hello World\" are not the expected ones"
            | _ -> failtest "The character stream does not begin with \"Hello\""
        }

        ptestProperty "Consuming the a character stream by a specified number of characters works as expected."
            (fun (CS(cs, length)) steps -> (uint32 steps < length && steps <> 0) ==> (fun () ->
                let mutable v = CharStream.view cs
                [1 .. steps]
                |> List.iter (fun _ ->
                    match v with
                    | CSCons (_, vs) -> v <- vs
                    | CSNil -> do())
                let s =
                    unpinSpanAndGenerate () (CharStreamCallback(fun _ _ x -> box <| x.ToString())) cs (pinSpan v)
                    |> fst
                    |> (fun x -> x :?> _)
                String.length s = steps))
    ]