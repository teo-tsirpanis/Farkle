// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Samples.FSharp.JSON

open System
open System.Globalization
open System.Text.Json.Nodes
open Farkle
open Farkle.Builder

let toDecimal (x: ReadOnlySpan<char>) =
    Decimal.Parse(
    #if NETCOREAPP
        x,
    #else
        x.ToString(),
    #endif
        NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture)
        |> JsonValue.Create
        :> JsonNode

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
            |> optional
        ]
        |> terminal "Number" (T(fun _ data -> toDecimal data))
    let string = Terminals.stringEx "/bfnrt" true false '"' "String"
    let object = nonterminal "Object"
    let array = nonterminal "Array"
    let value = "Value" ||= [
        !@ string => (fun str -> JsonValue.Create str :> JsonNode)
        !@ number |> asIs
        !@ object |> asIs
        !@ array |> asIs
        !& "true" => (fun () -> JsonValue.Create true :> JsonNode)
        !& "false" => (fun () -> JsonValue.Create false :> JsonNode)
        !& "null" =% null
    ]
    let arrayReversed: Nonterminal<JsonArray> = nonterminal "Array Reversed"
    arrayReversed.SetProductions(
        !@ arrayReversed .>> "," .>>. value => (fun xs x -> xs.Add x; xs),
        !@ value => (fun x -> let xs = JsonArray() in xs.Add(x); xs)
    )
    let arrayOptional = "Array Optional" ||= [
        !@ arrayReversed |> asIs
        empty => (fun () -> JsonArray())
    ]
    array.SetProductions(!& "[" .>>. arrayOptional .>> "]" => (fun x -> x :> JsonNode))

    let objectElement: Nonterminal<JsonObject> = nonterminal "Object Element"
    objectElement.SetProductions(
        !@ objectElement .>> "," .>>. string .>> ":" .>>. value => (fun xs k v -> xs.Add(k, v); xs),
        !@ string .>> ":" .>>. value => (fun k v -> let obj = JsonObject() in obj.Add(k, v); obj)
    )
    let objectOptional = "Object Optional" ||= [
        !@ objectElement |> asIs
        empty => (fun () -> JsonObject())
    ]
    object.SetProductions(!& "{" .>>. objectOptional .>> "}" => (fun x -> x :> JsonNode))

    value
    |> DesigntimeFarkle.caseSensitive true

[<CompiledName("Runtime")>]
let runtime = RuntimeFarkle.build designtime
