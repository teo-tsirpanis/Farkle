// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Farkle
open Farkle.Samples.FSharp
open System

let interactive parser =
    let rec impl() =
        eprintf "Your input: "
        let input = Console.ReadLine() |> Option.ofObj
        match input with
        | Some x ->
            CharParser.parseString parser x
            |> Console.WriteLine
            impl()
        | None -> ()
    eprintfn "This is a simple mathematical expression parser powered by Farkle."
    eprintfn "Written by Theodore Tsirpanis."
    eprintfn "Insert your expression and press enter."
    eprintfn "Press Control + C to exit."
    impl()

[<EntryPoint>]
let main args =
    let parser = SimpleMaths.int
    match args with
    | [| |] -> interactive parser
    | _ ->
        for x in args do
            CharParser.parseString parser x
            |> Console.WriteLine
    0
