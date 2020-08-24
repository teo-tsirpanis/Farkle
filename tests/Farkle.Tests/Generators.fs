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
open Farkle.Grammar.EGTFile
open Farkle.IO
open FsCheck
open SimpleMaths.SimpleMaths
open System.IO
open System.Text

let nonEmptyString = Arb.generate |> Gen.map (fun (NonEmptyString x) -> x)

let positionGen =
    Arb.generate
    |> Gen.three
    |> Gen.filter (fun (line, col, idx) -> line <> 0UL && col <> 0UL && line - 1UL + col - 1UL <= idx)
    |> Gen.map (fun (line, col, idx) -> {Line = line; Column = col; Index = idx})

let egtEntryGen =
    [
        Gen.constant Entry.Empty
        Arb.generate |> Gen.map Entry.Byte
        Arb.generate |> Gen.map Entry.Boolean
        Arb.generate |> Gen.map Entry.UInt32
        Arb.generate |> Gen.map (fun (NonNull str) -> Entry.String str)
    ]
    |> Gen.oneof

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
    let l = ResizeArray(arr.Length)
    while i < arr.Length do
        let! v = Arb.generate
        match! Arb.generate with
        // Make a range between the next two consecutive elements.
        | true when i < arr.Length - 1 ->
            l.Add(arr.[i], arr.[i + 1], v)
            i <- i + 2
        // Or add a single one.
        | _ ->
            l.Add(arr.[i], arr.[i], v)
            i <- i + 1
    return RangeMap l
}

let regexGen =
    let rec impl size = gen {
        if size <= 1 then
            return! nonEmptyString |> Gen.map Regex.chars
        else
            let gen = impl <| size / 2
            match! Gen.choose(0, 2) with
            | 0 -> return! Gen.map2 (<&>) gen gen
            | 1 -> return! Gen.map2 (<|>) gen gen
            | 2 when size >= 16 -> return! Gen.map Regex.chars nonEmptyString
            | _ -> return! Gen.map Regex.star gen
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
        | Regex.AllButChars x ->
            // This is our best shot here; creating the
            // complement is probably not a good idea.
            let! c = Arb.generate |> Gen.filter (x.Contains >> not)
            do sb.Append(c) |> ignore
        | Regex.Alt xs ->
            let! x = Gen.elements xs
            do! impl sb x
        | Regex.Concat xs ->
            for x in xs do
                do! impl sb x
        | Regex.Star x ->
            let! (NonNegativeInt len) = Arb.generate
            for __ = 0 to len - 1 do
                do! impl sb x
        // Regex strings are never created by the generator.
        | Regex.RegexString _ -> ()
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

let designtimeFarkleGen =
    let impl size = gen {
        let! terminals =
            Gen.choose(1, size)
            |> Gen.map (fun x ->
                Array.init x (sprintf "T%d" >> literal))
        let! (nonterminals : Untyped.Nonterminal[]) =
            Gen.choose(1, size)
            |> Gen.map (fun x ->
                Array.init x (sprintf "N%d" >> nonterminalU))
        let productionGen =
            Gen.oneof [
                Gen.elements terminals
                Gen.elements nonterminals |> Gen.map (fun x -> x :> DesigntimeFarkle)
            ]
            |> Gen.listOf
        for i = 0 to nonterminals.Length - 1 do
            let nont = nonterminals.[i]

            let! productions =
                Gen.nonEmptyListOf productionGen
                |> Gen.map (List.distinct >> List.map (List.fold (.>>) empty))
            match productions with
            | xs when i = 0 ->
                // We will force the grammar to derive at least one terminal
                // this way. GOLD Parser raises an error.
                nont.SetProductions(empty .>> terminals.[0], Array.ofList xs)
            | x :: xs ->
                nont.SetProductions(x, Array.ofList xs)
            | [] -> failwith "Impossible; the list was requested not to be empty."
        return nonterminals.[0] :> DesigntimeFarkle
    }
    Gen.sized impl
    // As the size of agrammar increases, it becomes more
    // and more likely for LALR conflicts to appear, making
    // the tests run for very long. I have no idea why FsCheck
    // does not raise an error though.
    |> Gen.resize 10
    |> Gen.filter (fun df ->
        let gDef = DesigntimeFarkleBuild.createGrammarDefinition df
        match DesigntimeFarkleBuild.buildGrammarOnly gDef with
        | Ok _ -> true
        | Result.Error _ -> false)

type FarkleVsGOLDParser = FarkleVsGOLDParser of farkleGramamr: Grammar * goldGrammar: Grammar

let farkleVsGOLDParserGen = gen {
    GOLDParserBridge.checkIfGOLDExists()
    let! gDef = Gen.map DesigntimeFarkleBuild.createGrammarDefinition Arb.generate
    let farkleGramamr =
        gDef
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> Flip.Expect.wantOk "A faulty grammar was supposed to be filtered away."
    let goldGrammar = GOLDParserBridge.buildUsingGOLDParser gDef
    return FarkleVsGOLDParser(farkleGramamr, goldGrammar)
}

type Generators =
    static member Terminal() = Gen.map2 (fun idx name -> Terminal(idx, name)) Arb.generate Arb.generate |> Arb.fromGen
    static member Position() = Arb.fromGen positionGen
    static member EGTEntry() = Arb.fromGen egtEntryGen
    static member AST() = Arb.fromGen <| ASTGen()
    static member RangeMap() = Arb.fromGen <| rangeMapGen()
    static member CS() = Arb.fromGen <| gen {
        let! str = nonEmptyString
        let! steps = Gen.choose(1, str.Length)
        let! generateStaticBlock = Arb.generate
        let charStream =
            if generateStaticBlock then
                CharStream str
            else
                StringReader(str) |> CharStream
        return CS(charStream, str, steps)
    }
    static member Regex() = Arb.fromGen regexGen
    static member Json() = Arb.fromGen JsonGen
    static member SimpleMathsAST() = Arb.fromGen simpleMathsASTGen
    static member Regexes() = Arb.fromGen regexesGen
    static member RegexStringPair() = Arb.fromGen regexStringPairGen
    static member DesigntimeFarkle() = Arb.fromGen designtimeFarkleGen
    static member FarkleVsGOLDParser() = Arb.fromGen farkleVsGOLDParserGen

let fsCheckConfig = {FsCheckConfig.defaultConfig with arbitrary = [typeof<Generators>]; replay = None}

let testProperty x = testPropertyWithConfig fsCheckConfig x
let ftestProperty x = ftestPropertyWithConfig fsCheckConfig x
let ptestProperty x = ptestPropertyWithConfig fsCheckConfig x

/// Performs a property test with a smaller sample size.
let testPropertySmall name prop = testPropertyWithConfigs {fsCheckConfig with endSize = 50} fsCheckConfig name prop
