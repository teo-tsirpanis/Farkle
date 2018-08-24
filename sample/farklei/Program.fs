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
    let parser = GOLDParser egtFile
    let result = parser.ParseFile(inputFile, GOLDParserConfig.Default)
    let print = if showOutput then printfn "%s" else ignore
    result.MessagesAsString |> Seq.iter print
    match result.Simple with
    | Ok x ->
        print "Reduction"
        x |> Reduction.drawReductionTree |> print
        print "AST"
        x |> AST.ofReduction |> AST.drawASCIITree |> print
        print "Simplified AST"
        x |> AST.ofReduction |> AST.simplify |> AST.drawASCIITree |> print
        0
    | Result.Error x ->
        print x
        1
