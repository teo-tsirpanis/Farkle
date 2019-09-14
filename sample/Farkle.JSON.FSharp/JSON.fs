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

let designtime =
    let number =
        Regex.concat [
            Regex.singleton '-' |> Regex.optional
            Regex.choice [
                Regex.singleton '-'
                Regex.singleton '0' <|> (Regex.oneOf "123456789" <&> (Regex.oneOf Number |> Regex.atLeast 0))
            ]
            Regex.optional <| (Regex.singleton '.' <&> (Regex.oneOf Number |> Regex.atLeast 1))
            [Regex.oneOf "eE"; Regex.oneOf "+-" |> Regex.optional; Regex.oneOf Number |> Regex.atLeast 1]
            |> Regex.concat
            |> Regex.optional]
        |> terminal "Number" (T(fun _ data -> toDecimal data))
    let stringCharacters = AllValid.Characters - (set ['"'; '\\'])
    let string =
        Regex.concat [
            Regex.singleton '"'
            Regex.atLeast 0 <| Regex.choice [
                Regex.oneOf stringCharacters
                Regex.concat [
                    Regex.singleton '\\'
                    Regex.choice [
                        Regex.oneOf "\"\\/bfnrt"
                        Regex.singleton 'u' <&> (Regex.repeat 4 <| Regex.oneOf "1234567890ABCDEF")
                    ]
                ]
            ]
            Regex.singleton '"'
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
