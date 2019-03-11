// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Argu
open Farkle.Tools.CreateSkeleton
open System

type FarkleCLIExiter() =
    interface IExiter with
        member __.Name = "Farkle CLI exiter"
        member __.Exit(msg, code) =
            Console.Error.WriteLine(msg)
            exit <| int code

type Arguments =
    | [<Inherit>] Version
    | [<CliPrefix(CliPrefix.None)>] Skeleton of ParseResults<SkeletonArguments>
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "display the program's version info."
            | Skeleton _ -> "generate a skeleton program from a grammar file and a Scriban template."

[<EntryPoint>]
let main _ =
    let parser = ArgumentParser.Create("farkle", "Help was requested", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    if results.Contains <@ Version @> then
        Console.WriteLine System.AssemblyVersionInformation.AssemblyVersion
    else
        match results.GetSubCommand() with
        | Skeleton args -> doSkeleton args
        | _ -> ()
    0
