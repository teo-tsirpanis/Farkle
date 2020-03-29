// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.PrecompilerTests

open Expecto
open Farkle.Builder
open Farkle.Builder.Precompiler
open System.Reflection

let _publicNumber = Terminals.int("Number").MarkForPrecompile()
let internal _internalNumber = Terminals.int("Internal Number").MarkForPrecompile()
let private _privateNumber = Terminals.int("Private Number").MarkForPrecompile()
module NestedModule =
    let _nestedNumber = Terminals.int("Nested Number").MarkForPrecompile()

// The following must not be discovered.

let _unmarkedNumber = Terminals.int("Number")
let _publicNumber2 = _publicNumber
let mutable _mutableNumber = Terminals.int("Mutable Number").MarkForPrecompile()

type MyTestClass() =
    member _._InstancePropertyNumber = Terminals.int("InstanceProperty Number").MarkForPrecompile()
    static member _StaticPropertyNumber = Terminals.int("Static Property Number").MarkForPrecompile()

[<Tests>]
let tests = testList "Precompiler tests" [
    test "The precompilable designtime Farkle discoverer works properly" {
        let actual =
            Assembly.GetExecutingAssembly()
            |> Discoverer.discover
            |> List.map (fun x -> x.Name)
            |> List.sort
        let expected =
            [_publicNumber; _internalNumber; _privateNumber; NestedModule._nestedNumber]
            |> List.map (fun df -> df.Name)
            |> List.sort
        Expect.sequenceEqual actual expected "The discovered designtime Farkles are not the right ones"
    }
]
