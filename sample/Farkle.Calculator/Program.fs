// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open SimpleMaths
open System

let prettyPrintResult =
    function
    | Ok x -> sprintf "%d" x
    | Error x -> sprintf "%O" x

let interactive () =
    let rec impl() = 
        let input = Console.ReadLine() |> Option.ofObj
        match input with
            | Some x -> 
                x
                |> TheRuntimeFarkle.ParseString
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
    | [||] -> interactive()
    | x -> x |> Array.iter (TheRuntimeFarkle.ParseString >> prettyPrintResult >> Console.WriteLine)
    0 // return an integer exit code
