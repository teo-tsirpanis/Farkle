// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Farkle
open System

let inline prettyPrintResult x =
    match x with
    | Ok x -> string x
    | Error x -> string x
    |> Console.WriteLine

let interactive rf =
    let rec impl() =
        eprintf "Your input: "
        let input = Console.ReadLine() |> Option.ofObj
        match input with
        | Some x ->
            RuntimeFarkle.parseString rf x
            |> prettyPrintResult
            impl()
        | None -> ()
    eprintfn "This is a simple mathematical expression parser powered by Farkle."
    eprintfn "Written by Theodore Tsirpanis."
    eprintfn "Insert your expression and press enter."
    eprintfn "Press Control + C to exit."
    impl()

[<EntryPoint>]
let main args =
    let rf = SimpleMaths.int
    match args with
    | [| |] -> interactive rf
    | [|"--ast"; x|] ->
        let rf = RuntimeFarkle.changePostProcessor PostProcessors.ast rf
        RuntimeFarkle.parseString rf x
        |> Result.map AST.toASCIITree
        |> prettyPrintResult
    | _ ->
        for x in args do
            RuntimeFarkle.parseString rf x
            |> prettyPrintResult
    0
