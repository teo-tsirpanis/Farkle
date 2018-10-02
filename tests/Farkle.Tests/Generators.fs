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
open System.Collections.Generic
open Farkle.Collections

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

let rangeMapGen() = gen {
    // Generate and sort an array of elements.
    let! arr = Arb.generate |> Gen.arrayOf |> Gen.map Array.distinct
    Array.sortInPlace arr
    let mutable i = 0
    let l = List(arr.Length)
    let buf = List(arr.Length)
    while i < arr.Length do
        match! Arb.generate with
        // Make a range between the next two consecutive elements.
        | true when i < arr.Length - 1 ->
            buf.Add(arr.[i], arr.[i + 1])
            i <- i + 2
        // Or add a single one.
        | _ ->
            buf.Add(arr.[i], arr.[i])
            i <- i + 1
        match! Arb.generate with
        | true ->
            do! Arb.generate |> Gen.map (fun x -> l.Add(buf.ToArray(), x))
            buf.Clear()
        | false -> ()
    let x = l.ToArray() |> RangeMap.ofRanges
    return x.Value
}

type Generators =
    static member Symbol() = Arb.fromGen symbolGen
    static member Token() = Gen.map3 Token.Create Arb.generate Arb.generate Arb.generate |> Arb.fromGen
    static member Position() = Arb.fromGen positionGen
    static member AST() = Arb.fromGen <| ASTGen()
    static member SetEx() =
        [
            Arb.generate |> Gen.map (Set.ofList >> SetEx.Set)
            Arb.generate |> Gen.map (fun (x1, x2) -> if x1 <= x2 then x1, x2 else x2, x1) |> Gen.listOf |> Gen.map SetEx.Range
        ]
        |> Gen.oneof
        |> Arb.fromGen
    static member RangeMap() = Arb.fromGen <| rangeMapGen()

let testProperty x = 
    testPropertyWithConfig
        {FsCheckConfig.defaultConfig with
            arbitrary = [typeof<Generators>]
            replay = None} x
