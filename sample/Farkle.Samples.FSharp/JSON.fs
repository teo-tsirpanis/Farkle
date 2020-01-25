// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.JSON.FSharp.Language

open System
open System.Globalization
open Chiron
open Farkle
open Farkle.Builder

let toDecimal (x: ReadOnlySpan<char>) =
    Decimal.Parse(
    #if NETCOREAPP3_1
        x,
    #else
        x.ToString(),
    #endif
        NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture)
        |> Json.Number

open Regex

let designtime =
    // Better let that regex stay.
    // JSON prohibits leading zeroes,
    // and we want to avoid boxing.
    let number =
        concat [
            char '-' |> optional
            choice [
                char '0'
                chars "123456789" <&> (chars Number |> star)
            ]
            optional <| (char '.' <&> (chars Number |> atLeast 1))
            [chars "eE"; chars "+-" |> optional; chars Number |> atLeast 1]
            |> concat
            |> optional]
        |> terminal "Number" (T(fun _ data -> toDecimal data))
    let string = Terminals.stringEx "/bfnrt" true false '"' "String"
    let object = nonterminal "Object"
    let array = nonterminal "Array"
    let value = "Value" ||= [
        !@ string => String
        !@ number => id
        !@ object => id
        !@ array => id
        !& "true" =% Bool true
        !& "false" =% Bool false
        !& "null" =% Null ()
    ]
    let arrayReversed = nonterminal "Array Reversed"
    arrayReversed.SetProductions(
        !@ arrayReversed .>> "," .>>. value => (fun xs x -> x :: xs),
        !@ value => List.singleton
    )
    let arrayOptional = "Array Optional" ||= [
        !@ arrayReversed => List.rev
        empty =% []
    ]
    array.SetProductions(!& "[" .>>. arrayOptional .>> "]" => Array)

    let objectElement = nonterminal "Object Element"
    objectElement.SetProductions(
        !@ objectElement .>> "," .>>. string .>> ":" .>>. value => (fun xs k v -> (k, v) :: xs),
        !@ string .>> ":" .>>. value => (fun k v -> [k, v])
    )
    let objectOptional = "Object Optional" ||= [
        !@ objectElement => (Map.ofList >> Object)
        empty =% (Object Map.empty)
    ]
    object.SetProductions(!& "{" .>>. objectOptional .>> "}" => id)

    value
    |> DesigntimeFarkle.caseSensitive true

let runtime = RuntimeFarkle.build designtime
