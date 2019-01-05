// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Farkle
open SimpleMaths
open System
open Farkle.PostProcessor

let inline prettyPrintResult x = tee string string x

let interactive rf =
    let rec impl() =
        let input = Console.ReadLine() |> Option.ofObj
        match input with
            | Some x ->
                RuntimeFarkle.parseString rf (string >> Console.Error.WriteLine) x
                |> prettyPrintResult
                |> Console.WriteLine
                impl()
            | None -> ()
    eprintfn "This is a simple mathematical expression parser powered by Farkle,"
    eprintfn "Copyright (c) 2018 Theodore Tsirpanis."
    eprintfn "Insert your expression and press enter."
    eprintfn "Press Control + C to exit."
    impl()

[<EntryPoint>]
let main args =
    let rf = TheRuntimeFarkle.Value
    match args with
    | [| |] -> interactive rf
    | [|"--ast"; x|] ->
        RuntimeFarkle.parseString (RuntimeFarkle.changePostProcessor PostProcessor.ast rf) Console.WriteLine x
        |> Result.map AST.toASCIITree
        |> prettyPrintResult
        |> Console.WriteLine
    | x -> x |> Array.iter (RuntimeFarkle.parseString rf Console.WriteLine >> prettyPrintResult >> Console.WriteLine)
    0 // return an integer exit code
