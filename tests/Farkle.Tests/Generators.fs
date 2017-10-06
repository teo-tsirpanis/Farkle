// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.Generators

open Chessie.ErrorHandling
open Expecto
open FsCheck
open Farkle
open Farkle.Grammar
open Farkle.Parser

let symbolGen = gen {
    let! name = Arb.generate
    let! index = Arb.generate
    let! symbolType = Arb.generate
    return {Name = name; Index = index; SymbolType = symbolType}
}

let symbolTypeGen = [SymbolType.Nonterminal; Terminal] |> Gen.elements

let productionGen = gen {
    let! index = Arb.generate
    let! head = Arb.generate
    let! handle = Arb.generate |> Gen.listOf
    return {Index = index; Head = head; Handle = handle}
}

let positionGen = Arb.generate |> Gen.filter ((<>) 0u) |> Gen.two |> Gen.map (uncurry Position.create >> mustBeSome)

let reductionGen =
    let rec impl size = gen {
        let! parent = Arb.generate
        let! tokens =
            let leafGen = Arb.generate |> Gen.map Choice1Of2 |> Gen.nonEmptyListOf
            match size with
            | size when size > 0 ->
                [
                    leafGen
                    impl (size / 2) |> Gen.map Choice2Of2 |> Gen.nonEmptyListOf
                ]
                |> Gen.oneof
            | _ -> leafGen
        return {Parent = parent; Tokens = tokens}
    }
    Gen.sized impl

let ASTGen() =
    let rec impl size =
        match size with
        | size when size > 0 ->
            let tree = impl (size / 2)
            [
                Gen.map2 (curry Content) Arb.generate Arb.generate
                Gen.map2 (curry Nonterminal) Arb.generate (Gen.nonEmptyListOf tree)
            ]
            |> Gen.oneof
        | _ -> Gen.map2 (curry Content) Arb.generate Arb.generate
    Gen.sized impl

type Generators =
    static member Symbol() = Arb.fromGen symbolGen
    static member SymbolType() = Arb.fromGen symbolTypeGen
    static member Position() = Arb.fromGen positionGen
    static member Reduction() = Arb.fromGen reductionGen
    static member AST() = Arb.fromGen (ASTGen())

let testProperty x = 
    testPropertyWithConfig
        {FsCheckConfig.defaultConfig with
            arbitrary = [typeof<Generators>]
            replay = None} x
