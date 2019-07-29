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
open Farkle.PostProcessor
open Farkle.JSON.FSharp.Definitions

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

// The transformers convert terminals to anything you want.
// If you do not care about a terminal (like single characters),
// you can remove it from below. It will be automatically ignored.
let private transformers =
    [
    #if NETCOREAPP2_1
        Transformer.create Terminal.Number <| C(fun x -> Decimal.Parse(x, NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture) |> Json.Number)
    #else
        Transformer.createS Terminal.Number (fun x -> Decimal.Parse(x, NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture) |> Json.Number)
    #endif
        Transformer.create Terminal.String <| C unescapeJsonString
    ]

open Fuser

let emptyMap: Map<string, Json> = Map.empty

// The fusers merge the parts of a production into one object of your desire.
// Do not delete anything here, or the post-processor will fail.
let private fusers =
    [
        take1Of Production.ValueString 0 Json.String
        identity Production.ValueNumber
        identity Production.ValueObject
        identity Production.ValueArray
        constant Production.ValueTrue <| Json.Bool true
        constant Production.ValueFalse <| Json.Bool false
        constant Production.ValueNull <| Json.Null ()
        take1Of Production.ArrayLBracketRBracket 1 Json.Array
        take1Of Production.ArrayOptionalArrayReversed 0 (List.rev: Json list -> _)
        constant Production.ArrayOptionalEmpty ([] : Json list)
        take2Of Production.ArrayReversedComma (2, 0) (fun (x: Json) xs -> x :: xs)
        take1Of Production.ArrayReversed 0 (List.singleton: Json -> _)
        take1Of Production.ObjectLBraceRBrace 1 (Map.ofList >> Json.Object)
        identity Production.ObjectOptionalObjectElement
        constant Production.ObjectOptionalEmpty ([] : (string * Json) list)
        take3Of Production.ObjectElementCommaStringColon (2, 4, 0) (fun (k: string) (v: Json) xs -> (k, v) :: xs)
        take2Of Production.ObjectElementStringColon (0, 2) (fun (k: string) (v: Json) -> [k, v])
    ]

let private createRuntimeFarkle() =
    RuntimeFarkle.ofBase64String
        (PostProcessor.ofSeq<Json> transformers fusers)
        Grammar.asBase64

let runtime = createRuntimeFarkle()
