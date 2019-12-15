// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.JSON.FSharp.Language

open System
open System.Globalization
open System.Text
open Chiron
open Farkle
open Farkle.Builder

let unescapeJsonString (x: ReadOnlySpan<_>) =
    let x = x.Slice(1, x.Length - 2)
    let mutable i = 0
    let sb = StringBuilder(x.Length)
    while i < x.Length do
        let c = x.[i]
        i <- i + 1
        match c with
        | '\\' ->
            let c = x.[i]
            i <- i + 1
            match c with
            | '\"' | '\\' | '/' -> sb.Append c |> ignore
            | 'b' -> sb.Append '\b' |> ignore
            | 'f' -> sb.Append '\f' |> ignore
            | 'n' -> sb.Append '\n' |> ignore
            | 'r' -> sb.Append '\r' |> ignore
            | 't' -> sb.Append '\t' |> ignore
            | 'u' ->
                let hexCode =
                #if NETCOREAPP2_1
                    UInt16.Parse(x.Slice(i, 4), NumberStyles.HexNumber)
                #else
                    UInt16.Parse(x.Slice(i, 4).ToString(), NumberStyles.HexNumber)
                #endif
                sb.Append(char hexCode) |> ignore
                i <- i + 4
            | _ -> ()
        | c -> sb.Append(c) |> ignore
    sb.ToString()

let toDecimal (x: ReadOnlySpan<char>) =
    #if NETCOREAPP2_1
        Decimal.Parse(x, NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture) |> Json.Number
    #else
        Decimal.Parse(x.ToString(), NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture) |> Json.Number
    #endif

open Regex

let designtime =
    let number =
        concat [
            char '-' |> optional
            choice [
                char '0'
                chars "123456789" <&> (chars Number |> atLeast 0)
            ]
            optional <| (char '.' <&> (chars Number |> atLeast 1))
            [chars "eE"; chars "+-" |> optional; chars Number |> atLeast 1]
            |> concat
            |> optional]
        |> terminal "Number" (T(fun _ data -> toDecimal data))
    let stringCharacters = AllValid.Characters - (set ['"'; '\\'])
    let string =
        concat [
            char '"'
            atLeast 0 <| choice [
                chars stringCharacters
                concat [
                    char '\\'
                    choice [
                        chars "\"\\/bfnrt"
                        char 'u' <&> (repeat 4 <| chars "1234567890ABCDEF")
                    ]
                ]
            ]
            char '"'
        ]
        |> terminal "String" (T(fun _ data -> unescapeJsonString data))
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
