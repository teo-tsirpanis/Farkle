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
open FsCheck
open System.Collections.Generic
open System.Text.Json.Nodes

let nonEmptyString = Arb.generate |> Gen.map (fun (NonEmptyString x) -> x)

let textPositionGen =
    Arb.generate
    |> Gen.two
    |> Gen.map (fun (line, col) -> TextPosition.Create0(line, col))

let JsonGen =
    let leaves =
        Gen.oneof [
            Arb.generate |> Gen.map JsonValue.Create<bool>
            Gen.constant <| null
            Arb.generate |> Gen.map JsonValue.Create<decimal>
            Arb.generate |> Gen.map (fun (NonNull str) -> JsonValue.Create<string> str)
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

let (|RegexAny|RegexChars|RegexAllButChars|RegexAlt|RegexConcat|RegexLoop|RegexRegexString|) (r: Regex) =
    let mutable chars = Unchecked.defaultof<_>
    let mutable isInverted = false
    let mutable stringLiteral = Unchecked.defaultof<_>
    let mutable regexes = Unchecked.defaultof<_>
    let mutable inner = null
    let mutable m = 0
    let mutable n = 0
    let mutable regexString = Unchecked.defaultof<_>
    if r.IsAny() then
        RegexAny
    elif r.IsChars(&chars, &isInverted) then
        if isInverted then
            RegexAllButChars chars
        else
            RegexChars chars
    // We can't add another case; F# supports only 7 cases in active patterns.
    // To add more, we will have to make a dedicated doscriminated union type
    // for regexes.
    elif r.IsStringLiteral(&stringLiteral) then
        stringLiteral
        |> Seq.map Regex.char
        |> _.ToImmutableArray()
        |> Choice5Of7 // RegexConcat but there is a bug in the compiler.
    elif r.IsAlt(&regexes) then
        RegexAlt regexes
    elif r.IsConcat(&regexes) then
        RegexConcat regexes
    elif r.IsLoop(&inner, &m, &n) then
        RegexLoop(inner, m, n)
    elif r.IsRegexString(&regexString) then
        RegexRegexString regexString.Pattern
    else
        failwith "Impossible"

let genRegexString regex =
    let containsInRanges xs x = xs |> Seq.exists (fun struct(a, b) -> a <= x && x <= b)
    let rec impl (sb: StringBuilder) regex = gen {
        match regex with
        | RegexAny ->
            let! c = Arb.generate<char>
            do sb.Append(c) |> ignore
        | RegexChars x ->
            let! c = Arb.generate |> Gen.filter (containsInRanges x)
            do sb.Append(c) |> ignore
        | RegexAllButChars x ->
            // This is our best shot here; creating the
            // complement is probably not a good idea.
            let! c = Arb.generate |> Gen.filter (containsInRanges x >> not)
            do sb.Append(c) |> ignore
        | RegexAlt xs ->
            let! x = Gen.elements xs
            do! impl sb x
        | RegexConcat xs ->
            for x in xs do
                do! impl sb x
        | RegexLoop(x, m, n) ->
            for __ = 0 to m - 1 do
                do! impl sb x
            let! (NonNegativeInt len) =
                Arb.generate
                |> if n = Int32.MaxValue then id else Gen.filter (fun (NonNegativeInt x) -> x <= n - m)
            for __ = 0 to len - 1 do
                do! impl sb x
        // Regex strings are never created by the generator.
        | RegexRegexString _ -> ()
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
    let! strings =
        regexSpec
        |> List.mapi (fun i x -> x |> genRegexString |> Gen.map (fun x -> x, i))
        |> Gen.sequence
    return Regexes(regexSpec, strings)
}

let regexStringPairGen = gen {
    let! regex = Arb.generate
    let! str = genRegexString regex
    return RegexStringPair(regex, str)
}

#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
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
#endif

type Generators =
    static member TextPosition() = Arb.fromGen textPositionGen
    static member Json() = Arb.fromGen JsonGen
    static member Regex() = Arb.fromGen regexGen
    static member Regexes() = Arb.fromGen regexesGen
    static member RegexStringPair() = Arb.fromGen regexStringPairGen
#if false // TODO-FARKLE7: Reevaluate when the builder is implemented in Farkle 7.
    static member SimpleMathsAST() = Arb.fromGen simpleMathsASTGen
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
