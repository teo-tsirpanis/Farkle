// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Chessie.ErrorHandling
open FSharpx.Collections
open Farkle
open Farkle.Grammar
open Farkle.Monads.StateResult
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
type GOLDParser =

    /// Parses a parser state.
    /// This is the lowest-level method.
    /// ## Parameters
    /// * `state`: The parser state.
    static member ParseState state =
        let rec impl() = sresult {
            let! pos = getOptic ParserState.CurrentPosition_
            let warn x = (x, pos) |> warn
            let fail x = (x, pos) |> fail
            let! x = parse() |> liftState
            match x with
            | Accept x ->
                do! warn <| Accept x
                return x
            | x when x |> ParseMessage.isError |> not ->
                do! warn x
                return! impl()
            | x ->
                return! x |> FatalError |> fail
        }
        eval (impl()) state

    /// Converts a parsing result to a result with human-readable error messages.
    /// ## Parameters
    /// * `result: The parsing result input.
    static member FormatErrors (result: Result<Reduction, ParseMessage * Position>) =
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

    /// Parses a string based on the given `Grammar` object, with an option to trim trivial reductions.
    /// Currently, the only way to create `Grammar`s is by using the functions in the `EGT` module.
    /// It's better to use this function if the same grammar is used multiple times, to avoid multiple grammars being constructed.
    /// ## Parameters
    /// * `grammar`: The `Grammar` object that contains the parsing logic.
    /// * `input`: The input string.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (grammar, input, trimReductions) = input |> List.ofString |> ParserState.create trimReductions grammar |> GOLDParser.ParseState

    /// Parses a string based on the grammar on the given EGT file, with an option to trim trivial reductions.
    /// Please note that _only_ EGT files are supported, _not_ CGT files (an error will be raised in that case).
    /// ## Parameters
    /// * `egtFile`: The path of the EGTfile that contains the parsing logic.
    /// * `input`: The input string.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (egtFile, input, trimReductions) = trial {
        let! grammar = EGT.ofFile egtFile |> Trial.mapFailure (fun x -> EGTReadError x, Position.initial)
        return! GOLDParser.Parse (grammar = grammar, input = input, trimReductions = trimReductions)
    }

    /// Parses a `Stream` based on the given `Grammar` object, with an option to trim trivial reductions.
    /// ## Parameters
    /// * `grammar`: The `Grammar` object that contains the parsing logic.
    /// * `inputStream`: The input stream.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (grammar, inputStream, trimReductions) =
        inputStream
        |> Seq.ofCharStream false
        |> List.ofSeq
        |> ParserState.create trimReductions grammar
        |> GOLDParser.ParseState
