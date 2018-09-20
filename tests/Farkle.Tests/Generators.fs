// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Generators

open Expecto
open FsCheck
open Farkle
open Farkle.Grammar
open Farkle.Parser

let symbolGen = gen {
    let! name = Arb.generate
    let! symbolType = Gen.elements [Symbol.Nonterminal; Terminal]
    return symbolType name
}

let productionGen = gen {
    let! index = Arb.generate
    let! head = Arb.generate
    let! handle = Arb.generate |> Gen.listOf
    return {Index = index; Head = head; Handle = handle}
}

let positionGen = Arb.generate |> Gen.filter ((<>) 0u) |> Gen.two |> Gen.map (uncurry Position.create >> mustBeSome)

let ASTGen() =
    let rec impl size =
        match size with
        | size when size > 0 ->
            let tree = impl (size / 2)
            [
                Gen.map AST.Content Arb.generate
                Gen.map2 (curry AST.Nonterminal) Arb.generate (Gen.nonEmptyListOf tree)
            ]
            |> Gen.oneof
        | _ -> Gen.map AST.Content Arb.generate
    Gen.sized impl

type Generators =
    static member Symbol() = Arb.fromGen symbolGen
    static member Token() = Gen.map3 Token.Create Arb.generate Arb.generate Arb.generate |> Arb.fromGen
    static member Position() = Arb.fromGen positionGen
    static member AST() = Arb.fromGen (ASTGen())
    static member SetEx() =
        [
            Arb.generate |> Gen.map (Set.ofList >> SetEx.Set)
            Arb.generate |> Gen.map (fun (x1, x2) -> if x1 <= x2 then x1, x2 else x2, x1) |> Gen.listOf |> Gen.map SetEx.Range
        ]
        |> Gen.oneof
        |> Arb.fromGen

let testProperty x = 
    testPropertyWithConfig
        {FsCheckConfig.defaultConfig with
            arbitrary = [typeof<Generators>]
            replay = None} x
