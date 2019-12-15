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
open Farkle.Tests.GOLDParserBridge
open FsCheck
open SimpleMaths
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Text

let nonEmptyString = Arb.generate |> Gen.map (fun (NonEmptyString x) -> x)

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
            match! Gen.choose(0, 2) with
            | 0 -> return! Gen.map2 (<&>) gen gen
            | 1 -> return! Gen.map2 (<|>) gen gen
            | 2 when size >= 16 -> return! Gen.map Regex.oneOf nonEmptyString
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

type Regexes = Regexes of (Regex * DFASymbol) list * (string * DFASymbol) list

type RegexStringPair = RegexStringPair of Regex * string

let genRegexString regex =
    let rec impl (sb: StringBuilder) regex = gen {
        match regex with
        | Regex.Chars x ->
            let! c = Gen.elements x
            do sb.Append(c) |> ignore
        | Regex.Alt xs ->
            let! x = Gen.elements xs
            do! impl sb x
        | Regex.Concat xs ->
            for x in xs do
                do! impl sb x
        | Regex.Star x ->
            let! len = Arb.generate
            for __ = 0 to len - 1 do
                do! impl sb x
    }
    gen {
        let sb = StringBuilder()
        do! impl sb regex
        return sb.ToString()
    }

let regexesGen = gen {
    let! regexSpec =
        Arb.generate
        |> Gen.nonEmptyListOf
        |> Gen.map (List.mapi (fun idx regex ->
            let symbol = Choice1Of4 <| Terminal(uint32 idx, sprintf "Terminal %d" idx)
            regex, symbol))
    let! strings =
        regexSpec
        |> List.map (fun (regex, symbol) -> regex |> genRegexString |> Gen.map (fun x -> x, symbol))
        |> Gen.sequence
    return Regexes(regexSpec, strings)
}

let regexStringPairGen = gen {
    let! regex = Arb.generate
    let! str = genRegexString regex
    return RegexStringPair(regex, str)
}

let grammarGen =
    let impl size = gen {
        let! terminals =
            Gen.choose(1, size)
            |> Gen.map (fun x ->
                Array.init x (fun idx ->
                    let idx = uint32 idx
                    Terminal(uint32 idx, sprintf "T%d" idx)))
        let! nonterminals =
            Gen.choose(1, size)
            |> Gen.map (fun x ->
                Array.init x (fun idx ->
                    let idx = uint32 idx
                    Nonterminal(uint32 idx, sprintf "N%d" idx)))
        let handleGen =
            Gen.oneof [
                Gen.elements terminals |> Gen.map LALRSymbol.Terminal
                Gen.elements nonterminals |> Gen.map LALRSymbol.Nonterminal
            ]
            |> Gen.arrayOf
            |> Gen.map ImmutableArray.CreateRange
        let! productionPairs =
            nonterminals
            |> Gen.collect (fun nont ->
                handleGen |> Gen.nonEmptyListOf |> Gen.map (List.map (fun handle -> nont, handle)))
            |> Gen.map List.concat
        let productions = ImmutableArray.CreateBuilder()
        (nonterminals.[0], ImmutableArray.Create(LALRSymbol.Terminal terminals.[0])) :: productionPairs
        |> List.iteri (fun idx (head, handle) -> productions.Add{Index = uint32 idx; Head = head; Handle = handle})
        return GrammarDefinition(nonterminals.[0], productions.ToImmutable())
    }
    Gen.sized impl |> Gen.filter (fun (GrammarDefinition(startSymbol, productions)) ->
        match LALRBuild.buildProductionsToLALRStates startSymbol productions with
        | Ok _ -> true
        | Result.Error _ -> false)

type Generators =
    static member Terminal() = Gen.map2 (fun idx name -> Terminal(idx, name)) Arb.generate Arb.generate |> Arb.fromGen
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
    static member Json() = Arb.fromGen JsonGen
    static member SimpleMathsAST() = Arb.fromGen simpleMathsASTGen
    static member Regexes() = Arb.fromGen regexesGen
    static member RegexStringPair() = Arb.fromGen regexStringPairGen
    static member GrammarDefinition() = Arb.fromGen grammarGen

let fsCheckConfig = {FsCheckConfig.defaultConfig with arbitrary = [typeof<Generators>]; replay = None}

let testProperty x = testPropertyWithConfig fsCheckConfig x
