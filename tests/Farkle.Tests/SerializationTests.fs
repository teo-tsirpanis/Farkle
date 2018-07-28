// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.SerializationTests

open Expecto
open Farkle
open Farkle.RuntimeHelpers.Serialization
open Generators
open Farkle.Grammar

let testIt<'a when 'a: equality> typeName = testProperty (sprintf "Serializing %ss works and the other way around" typeName) (fun (x: 'a) ->
    let mashedX = serialize x
    let xAgain = deserialize<'a> mashedX
    xAgain = Ok x)

[<Tests>]
let tests =
    testList "Serializer tests" [
        testIt<string> "string"
        testIt<int> "32-bit integer"
        // testIt<RuntimeGrammar> "runtime grammar"

        ptest "The inception grammar gets serialized" {
            let rtg = "inception.egt" |> EGT.ofFile |> Result.map RuntimeGrammar.ofGOLDGrammar |> returnOrFail
            let mashedRTG = serialize rtg
            let rtgAgain = deserialize mashedRTG |> returnOrFail
            Expect.equal rtgAgain rtg "The serialization of the inception grammar did not round-trip."
        }
    ]