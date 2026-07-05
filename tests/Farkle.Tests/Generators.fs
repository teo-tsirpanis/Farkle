// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Generators

open System
open System.Collections.Immutable
open System.Text
open Expecto
open Farkle
open Farkle.Builder
open Farkle.Samples.FSharp.SimpleMaths
open FsCheck
open FsCheck.FSharp
open System.Collections.Generic
open System.Text.Json.Nodes

let nonEmptyString = ArbMap.defaults |> ArbMap.generate |> Gen.map (fun (NonEmptyString x) -> x)

let textPositionGen =
    ArbMap.defaults
    |> ArbMap.generate
    |> Gen.two
    |> Gen.map (fun (line, col) -> TextPosition.Create0(line, col))

let JsonGen =
    let leaves =
        Gen.oneof [
            ArbMap.defaults |> ArbMap.generate |> Gen.map JsonValue.Create<bool>
            Gen.constant <| null
            ArbMap.defaults |> ArbMap.generate |> Gen.map JsonValue.Create<decimal>
            ArbMap.defaults |> ArbMap.generate |> Gen.map (fun (NonNull str) -> JsonValue.Create<string> str)
        ]
        |> Gen.map (fun x -> x :> JsonNode)
    let branches items =
        Gen.oneof [
            items
            |> Gen.arrayOf
            |> Gen.map (fun x -> JsonArray x :> JsonNode)

            Gen.zip nonEmptyString items
            |> Gen.map KeyValuePair.Create
            |> Gen.listOf
            |> Gen.map (List.distinctBy (fun x -> x.Key))
            |> Gen.map (fun xs -> JsonObject xs :> JsonNode)
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

let regexGen =
    let rec impl size = gen {
        if size <= 1 then
            // Generating inverted character sets presents many challenges,
            // including difficulty in generating a string that matches them,
            // and generating case-insensitive regexes, so we will not do it
            // at least for now.
            return! nonEmptyString |> Gen.map Regex.chars
        else
            let gen = impl <| size / 2
            match! Gen.choose(0, 2) with
            | 0 -> return! Gen.map2 (+) gen gen
            | 1 -> return! Gen.map2 (|||) gen gen
            | 2 when size >= 16 -> return! Gen.map Regex.chars nonEmptyString
            | _ -> return! Gen.map Regex.plus gen
    }
    Gen.sized impl

type Regexes = Regexes of Regex list * (string * int) list

type RegexStringPair = RegexStringPair of Regex * string

let (|RegexAny|_|) (r: Regex) = r.IsAny()

let (|RegexChars|_|) (r: Regex) =
    match r.IsChars() with
    | true, chars, flags -> ValueSome(chars, flags.HasFlag Regex.CharsFlags.Inverted)
    | false, _, _ -> ValueNone

let (|RegexCharRanges|_|) (r: Regex) =
    match r.IsCharRanges() with
    | true, ranges, flags -> ValueSome(ranges, flags.HasFlag Regex.CharsFlags.Inverted)
    | false, _, _ -> ValueNone

let (|RegexStringLiteral|_|) (r: Regex) =
    match r.IsStringLiteral() with
    | true, str -> ValueSome str
    | false, _ -> ValueNone

let (|RegexAlt|_|) (r: Regex) =
    match r.IsAlt() with
    | true, regexes -> ValueSome regexes
    | false, _ -> ValueNone

let (|RegexConcat|_|) (r: Regex) =
    match r.IsConcat() with
    | true, regexes -> ValueSome regexes
    | false, _ -> ValueNone

let (|RegexLoop|_|) (r: Regex) =
    match r.IsLoop() with
    | true, inner, m, n -> ValueSome(inner, m, n)
    | false, _, _, _ -> ValueNone

let genRegexString regex =
    let rec impl (sb: StringBuilder) regex = gen {
        match regex with
        | RegexAny ->
            let! c = ArbMap.defaults |> ArbMap.generate<char>
            sb.Append c |> ignore
        | RegexChars (xs, isInverted) ->
            let! c = ArbMap.defaults |> ArbMap.generate |> Gen.filter (fun c -> xs.Span.Contains c <> isInverted)
            sb.Append c |> ignore
        | RegexAlt xs ->
            let! x = Gen.elements xs
            do! impl sb x
        | RegexConcat xs ->
            for x in xs do
                do! impl sb x
        | RegexLoop(x, m, n) ->
            for __ = 0 to m - 1 do
                do! impl sb x
            let! NonNegativeInt len =
                ArbMap.defaults
                |> ArbMap.generate
                |> if n = Int32.MaxValue then id else Gen.filter (fun (NonNegativeInt x) -> x <= n - m)
            for __ = 0 to len - 1 do
                do! impl sb x
        | _ -> failwith "Unsupported regex type in generator"
    }
    gen {
        let sb = StringBuilder()
        do! impl sb regex
        return sb.ToString()
    }

let regexesGen = gen {
    let! regexSpec =
        regexGen
        |> Gen.nonEmptyListOf
    let! strings =
        regexSpec
        |> List.mapi (fun i x -> x |> genRegexString |> Gen.map (fun x -> x, i))
        |> Gen.sequenceToList
    return Regexes(regexSpec, strings)
}

let regexStringPairGen = gen {
    let! regex = regexGen
    let! str = genRegexString regex
    return RegexStringPair(regex, str)
}

let simpleMathsASTGen =
    let rec impl size =
        if size <= 1 then
            ArbMap.defaults |> ArbMap.generate |> Gen.map (Number >> MathExpression.Create)
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

#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
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
                Gen.elements nonterminals |> Gen.map (fun x -> x :> IGrammarSymbol)
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
                setProductionsU nont <| (empty .>> terminals[0]) ::xs
            | _ :: _ ->
                setProductionsU nont productions
            | [] -> failwith "Impossible; the list was requested not to be empty."
        return nonterminals.[0] :> IGrammarSymbol
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
#endif

type Generators =
    static member TextPosition() = Arb.fromGen textPositionGen
    static member Json() = Arb.fromGen JsonGen
    static member Regex() = Arb.fromGen regexGen
    static member Regexes() = Arb.fromGen regexesGen
    static member RegexStringPair() = Arb.fromGen regexStringPairGen
    static member SimpleMathsAST() = Arb.fromGen simpleMathsASTGen
#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
    static member DesigntimeFarkle() = Arb.fromGen designtimeFarkleGen
#endif

let fsCheckConfig = {FsCheckConfig.defaultConfig with arbitrary = [typeof<Generators>]; replay = None}

let testProperty x = testPropertyWithConfig fsCheckConfig x
let ftestProperty x = ftestPropertyWithConfig fsCheckConfig x
let ptestProperty x = ptestPropertyWithConfig fsCheckConfig x

/// Performs a property test with a smaller sample size.
let testPropertySmall name prop = testPropertyWithConfigs {fsCheckConfig with endSize = 50} fsCheckConfig name prop
let ftestPropertySmall name prop = ftestPropertyWithConfigs {fsCheckConfig with endSize = 50} fsCheckConfig name prop
let ptestPropertySmall name prop = ptestPropertyWithConfigs {fsCheckConfig with endSize = 50} fsCheckConfig name prop
