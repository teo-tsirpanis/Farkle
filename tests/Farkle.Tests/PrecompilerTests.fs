// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.PrecompilerTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.Precompiler
open System.Reflection

// The following must be discovered.

let publicNumber = Terminals.int("Number").MarkForPrecompile()
let internal internalNumber = Terminals.int("Internal Number").MarkForPrecompile()
let private privateNumber = Terminals.int("Private Number").MarkForPrecompile()
module NestedModule =
    let nestedNumber = Terminals.int("Nested Number").MarkForPrecompile()
let markedAgain = RuntimeFarkle.dummyPrecompilable.Rename("Marked Again").MarkForPrecompile()

// The following must not be discovered.
let unmarkedNumber = Terminals.int("Number")
let differentAssembly = RuntimeFarkle.dummyPrecompilable
let publicNumber2 = publicNumber

// And the following must not even be evaluated.
module MustNotTouch =
    let mustNotTouch reason: DesigntimeFarkle =
        Terminals.int reason |> RuntimeFarkle.markForPrecompile :> _
        // We can't actually fail the test when these values
        // were evaluated because of https://github.com/dotnet/fsharp/issues/9719
        // failtestf "The discoverer must not touch designtime Farkles that %s." reason
    let _startsWithUnderscore = mustNotTouch "start with underscore"
    let mutable mutableNumber = mustNotTouch "are mutable"

    type MyTestClass() =
        member _.InstancePropertyNumber = mustNotTouch "are declared in an instance property"
        static member StaticPropertyNumber = mustNotTouch "are declared in a static property"

[<Tests>]
let tests = testList "Precompiler tests" [
    test "The precompilable designtime Farkle discoverer works properly" {
        let actual =
            Assembly.GetExecutingAssembly()
            |> Discoverer.discover
            |> List.map (fun x -> x.Name)
            |> List.sort
        let expected =
            [publicNumber; internalNumber; privateNumber; NestedModule.nestedNumber; markedAgain]
            |> List.map (fun df -> df.Name)
            |> List.sort
        Expect.sequenceEqual actual expected "The discovered designtime Farkles are not the right ones"
    }
]
