// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Text
open Chessie.ErrorHandling
open Argu
open Farkle
open Farkle.Grammar
open Farkle.Parser

type Arguments =
    | [<ExactlyOnce>] EGTFile of string
    | [<ExactlyOnce>] InputFile of string
    | [<Unique>] TrimReductions
    | [<Unique; AltCommandLine("-e")>] EagerRead
    | [<Unique; AltCommandLine("-o")>] ShowOutput
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | EGTFile _ -> "the file containing the grammar to be parsed"
            | InputFile _ -> "the file to be parsed"
            | TrimReductions -> "simplify reductions that have only one nonterminal"
            | EagerRead -> "read all input at once"
            | ShowOutput -> "show output to console. Setting it may hurt performance"



[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "farklei")
    let args = parser.Parse argv
    let egtFile = args.GetResult <@ EGTFile @>
    let inputFile = args.GetResult <@ InputFile @>
    let trimReductions = args.Contains <@ TrimReductions @>
    let isLazy = args.Contains <@ EagerRead @> |> not
    let showOutput = args.Contains <@ ShowOutput @>
    let grammar = EGT.ofFile egtFile |> Trial.mapFailure EGTReadError |> returnOrFail
    let inputStream = File.OpenRead inputFile
    let result, log = GOLDParser.Parse(grammar, inputStream, false, isLazy, trimReductions) |> GOLDParser.FormatErrors
    let print = if showOutput then printfn "%s" else ignore
    log |> Array.iter print
    match result with
    | Choice1Of2 x ->
        x |> Reduction.drawReductionTree |> print
        0
    | Choice2Of2 x ->
        print x
        1
