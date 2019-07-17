// This file was created by Farkle.Tools and is a skeleton
// to help you write a post-processor for JSON.
// You should complete it yourself, and keep it to source control.

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
        Transformer.create Terminal.Number <| C(fun x -> Decimal.Parse x |> Json.Number)
    #else
        Transformer.createS Terminal.Number (fun x -> Decimal.Parse x |> Json.Number)
    #endif
        Transformer.create Terminal.String <| C unescapeJsonString
    ]

open Fuser

// The fusers merge the parts of a production into one object of your desire.
// Do not delete anything here, or the post-processor will fail.
let private fusers =
    [
        identity Production.ValueString
        identity Production.ValueNumber
        identity Production.ValueObject
        identity Production.ValueArray
        constant Production.ValueTrue <| Json.Bool true
        constant Production.ValueFalse <| Json.Bool false
        constant Production.ValueNull <| Json.Null ()
        take1Of Production.ArrayLBracketRBracket 1 Json.Array
        take2Of Production.ArrayElementComma (0, 2) (fun (x: Json) xs -> x :: xs)
        constant Production.ArrayElementEmpty ([] : Json list)
        take1Of Production.ObjectLBraceRBrace 1 Json.Object
        take3Of Production.ObjectElementStringColonComma (0, 2, 4) (fun (k: string) (v: Json) xs -> Map.add k v xs)
        constant Production.ObjectElementEmpty (Map.empty: Map<string,Json>)
    ]

let private createRuntimeFarkle() =
    RuntimeFarkle.ofBase64String
        (PostProcessor.ofSeq<Json> transformers fusers)
        Grammar.asBase64

let runtime = createRuntimeFarkle()
