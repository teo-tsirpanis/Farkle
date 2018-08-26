// Learn more about F# at http://fsharp.org

open Argu
open Farkle
open Farkle.Parser

type Arguments =
    | [<ExactlyOnce>] EGTFile of string
    | [<ExactlyOnce>] InputFile of string
    | [<Unique; AltCommandLine("-s")>] Silent
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | EGTFile _ -> "the file containing the grammar to be parsed"
            | InputFile _ -> "the file to be parsed"
            | Silent -> "do not show any output at all"

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "farklei")
    let args = parser.Parse argv
    let egtFile = args.GetResult <@ EGTFile @>
    let inputFile = args.GetResult <@ InputFile @>
    let showOutput = args.Contains <@ Silent @> |> not
    let gp = GOLDParser.ofEGTFile egtFile
    let print x = if showOutput then printfn "%O" x else ignore x
    let result = GOLDParser.parseFile gp (print) GOLDParserConfig.Default inputFile
    match result with
    | Ok x ->
        print "AST"
        x |> AST.toASCIITree |> print
        print "Simplified AST"
        x |> AST.simplify |> AST.toASCIITree |> print
        0
    | Result.Error x ->
        print x
        1
