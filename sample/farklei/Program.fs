// Learn more about F# at http://fsharp.org

open Argu
open Farkle
open Farkle.Grammar.GOLDParser
open Farkle.Parser

type Arguments =
    | [<ExactlyOnce>] EGTFile of string
    | [<ExactlyOnce>] InputFile of string
    | [<Unique; AltCommandLine("-s")>] Silent
    | [<Unique>] LazyLoad of bool
    | [<Unique>] JustLoadEGT
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | EGTFile _ -> "the file containing the grammar to be parsed"
            | InputFile _ -> "the file to be parsed"
            | Silent -> "do not show any output at all"
            | LazyLoad _ -> "lazily load input"
            | JustLoadEGT -> "just load the grammar file; do not parse the grammar at all"

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "farklei")
    let args = parser.Parse argv
    let egtFile = args.GetResult <@ EGTFile @>
    let inputFile = args.GetResult <@ InputFile @>
    let showOutput = args.Contains <@ Silent @> |> not
    let lazyLoad = args.TryGetResult <@ LazyLoad @> |> Option.defaultValue true
    let justLoadEGT = args.Contains <@ JustLoadEGT @>
    let g = EGT.ofFile egtFile |> returnOrFail
    let print x = if showOutput then printfn "%O" x
    if not justLoadEGT then
        let result = GOLDParser.parseFile g (print) lazyLoad System.Text.Encoding.UTF8 inputFile
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
    else
        0
