// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Generators

open Chiron
open Expecto
open Farkle
open Farkle.Builder
open Farkle.Collections
open Farkle.Grammar
open Farkle.IO
open FsCheck
open SimpleMaths
open System.Collections.Generic
open System.Collections.Immutable
open System.IO

let nonEmptyString = Arb.generate |> Gen.map (fun (NonEmptyString x) -> x)

let productionGen = gen {
    let! index = Arb.generate
    let! head = Arb.generate
    let! handle = Arb.generate |> Gen.listOf
    return {Index = index; Head = head; Handle = ImmutableArray.CreateRange handle}
}

let positionGen =
    Arb.generate
    |> Gen.three
    |> Gen.filter (fun (line, col, idx) -> line <> 0UL && col <> 0UL && line - 1UL + col - 1UL <= idx)
    |> Gen.map (fun (line, col, idx) -> {Line = line; Column = col; Index = idx})

let ASTGen() =
    let rec impl size =
        match size with
        | size when size > 0 ->
            let tree = impl (size / 2)
            [
                Gen.map AST.Content Arb.generate
                Gen.map2 (fun prod tree -> AST.Nonterminal(prod, tree)) Arb.generate (Gen.nonEmptyListOf tree)
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

let regexGen =
    let rec impl size = gen {
        if size <= 1 then
            return! nonEmptyString |> Gen.map Regex.oneOf
        else
            let gen = impl <| size / 2
            match! Gen.choose(0, 3) with
            | 0 -> return! nonEmptyString |> Gen.map Regex.oneOf
            | 1 -> return! Gen.map2 (<&>) gen gen
            | 2 -> return! Gen.map2 (<|>) gen gen
            | _ -> return! Gen.map (Regex.atLeast 0) gen
    }
    Gen.sized impl

let JsonGen =
    let leaves =
        Gen.oneof [
            Arb.generate |> Gen.map Bool
            Gen.constant <| Null ()
            Arb.generate |> Gen.map Json.Number
            Arb.generate |> Gen.map (fun (NonNull str) -> String str)
        ]
    let branches items =
        Gen.oneof [
            items |> Gen.nonEmptyListOf |> Gen.map Array
            Gen.zip nonEmptyString items |> Gen.nonEmptyListOf |> Gen.map (Map.ofList >> Object)
        ]
    let rec impl size =
        if size <= 0 then
            leaves
        else
            Gen.oneof [
                leaves
                size / 2 |> impl |> branches
            ]
    Gen.sized (impl >> branches)

let simpleMathsASTGen =
    let rec impl size =
        if size <= 1 then
            Arb.generate |> Gen.map (Number >> MathExpression.Create)
        else gen {
            let! leftExprSize = Gen.choose(1, size)
            let rightExprSize = size - leftExprSize
            let! x1 = impl leftExprSize
            if rightExprSize = 0 then
                return x1 |> Negate |> MathExpression.Create
            else
                let! x2 = impl rightExprSize
                return! Gen.elements <| List.map MathExpression.Create [
                    yield Add(x1, x2)
                    yield Subtract(x1, x2)
                    yield Multiply(x1, x2)
                    if x2.Value <> 0 then
                        yield Divide(x1, x2)
                ]
        }
    Gen.sized impl

type CS = CS of CharStream * string * steps:int

type Generators =
    static member Terminal() = Gen.map2 (fun idx name -> Terminal(idx, name)) Arb.generate Arb.generate |> Arb.fromGen
    static member Production() = Arb.fromGen productionGen
    static member Position() = Arb.fromGen positionGen
    static member AST() = Arb.fromGen <| ASTGen()
    static member RangeMap() = Arb.fromGen <| rangeMapGen()
    static member CS() = Arb.fromGen <| gen {
        let! str = nonEmptyString
        let! steps = Gen.choose(1, str.Length)
        let! generateStaticBlock = Arb.generate
        let charStream =
            if generateStaticBlock then
                CharStream.ofString str
            else
                new StringReader(str) |> CharStream.ofTextReader
        return CS(charStream, str, steps)
    }
    static member Regex() = Arb.fromGen regexGen
    static member Json() = Arb.fromGen <| JsonGen
    static member SimpleMathsAST() = Arb.fromGen <| simpleMathsASTGen

let fsCheckConfig = {FsCheckConfig.defaultConfig with arbitrary = [typeof<Generators>]; replay = None}

let testProperty x = testPropertyWithConfig fsCheckConfig x
