// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Chessie.ErrorHandling
open FSharpx.Collections
open Farkle
open Farkle.Grammar
open Farkle.Parser
open Farkle.Parser.Implementation
open System
open System.IO
open System.Text

/// Functions that parse strings according to a specific grammar.
/// This is the definitive reference for all users of the library.
/// * If the function succeeds, its return value is the top reduction of the grammar.
/// * If it fails, the first message explains the reason it failed.
/// The rest of the messages are a kind of a log of the parser.
/// The term "trivial reductions" means a reduction that has only one nonterminal.
/// They can optionally be trimmed, so that the resulting parse tree is simplified.
type GOLDParser(grammar, trimReductions) =

    let newParser = GOLDParser.CreateParser trimReductions grammar

    new (grammar: Grammar) = GOLDParser(grammar = grammar, trimReductions = false)

    new (egtFile) =
        let grammar = egtFile |> EGT.ofFile |> returnOrFail
        GOLDParser grammar

    new (egtFile, trimReductions) =
        let grammar = egtFile |> EGT.ofFile |> returnOrFail
        GOLDParser(grammar, trimReductions)

    static member CreateParser trimReductions grammar input = createParser trimReductions grammar input

    /// Evaluates a `Parser` unitl it either succeeds or fails.
    member x.ParseChars input =
        let warn x = warn x ()
        let rec impl p = trial {
            match p with
            | Started x -> return! impl x.Value
            | Continuing (msg, x) ->
                do! warn msg
                return! impl x.Value
            | Failed (pos, msg) ->
                return! (pos, FatalError msg) |> fail
            | Finished (pos, x) ->
                do! (pos, Accept x) |> warn
                return x
        }
        input |> newParser |> impl

    /// Parses a string.
    member x.ParseString input = input |> List.ofString |> x.ParseChars

    /// Parses a `Stream`. Its character encoding is automatically detected.
    member x.ParseStream inputStream =
        inputStream
        |> Seq.ofCharStream false
        |> List.ofSeq
        |> x.ParseChars

    member x.ParseFile path =
        use stream = File.OpenRead path
        x.ParseStream stream

    /// Converts a parsing result to a result with human-readable error messages.
    static member FormatErrors (result: Result<Reduction, Position * ParseMessage>) =
        let result = result |> Trial.mapFailure (fun (msg, pos) -> sprintf "%O %O" msg pos)
        let messages =
            match result with
            | Ok (_, messages) -> messages
            | Bad (_ :: messages) -> messages
            | Bad [] -> []
            |> Array.ofList
        match result with
        | Ok (r, _) ->
            Choice1Of2 r
        | Bad (error :: _) -> Choice2Of2 error
        | Bad [] -> Choice2Of2 ""
        , messages
