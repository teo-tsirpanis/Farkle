﻿// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Farkle
open Farkle.Parser
open SimpleMaths
open System

let prettyPrintResult =
    function
    | Ok x -> sprintf "%O" x
    | Error x -> sprintf "%O" x

let interactive () =
    let rec impl() = 
        let input = Console.ReadLine() |> Option.ofObj
        match input with
            | Some x -> 
                x
                |> RuntimeFarkle.parseString TheRuntimeFarkle
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
    match args with
    | [| |] -> interactive()
    | [|"--ast"; x|] ->
        RuntimeFarkle.asGOLDParser TheRuntimeFarkle
        |> Result.bind (fun gp -> gp.ParseString(x).Value |> fst |> Result.mapError ParseError)
        |> Result.map (AST.ofReduction >> AST.drawASCIITree)
        |> prettyPrintResult
        |> Console.WriteLine
    | x -> x |> Array.iter (RuntimeFarkle.parseString TheRuntimeFarkle >> prettyPrintResult >> Console.WriteLine)
    0 // return an integer exit code
