// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Parser
open Farkle.Parser.Internal
open System.IO

/// A dedicated type to signify the result of a parser.
type ParseResult =
    /// Parsing succeeded. The final `Reduction` and the parsing log are returned.
    | Success of reduction: Reduction * messages: ParseMessage seq
    /// Parsing failed, The fatal `ParseMessage` is separately returned.
    | Failure of fatalMessage: ParseMessage * messages: ParseMessage seq
    /// The parsing log. In case of failure, the fatal `ParseMessage` is _not_ included.
    member x.Messages =
        match x with
        | Success (_, x) -> x
        | Failure (_, x) -> x
    /// The parsing log in human-friendly format.
    member x.MessagesAsString = x.Messages |> Seq.map string
    /// A simple `Choice` with either the final `Reduction` or the fatal `ParseMessage` as a string.
    member x.Simple =
        match x with
        | Success (x, _) -> Choice1Of2 x
        | Failure (x, _) -> x |> string |> Choice2Of2
    /// Returns the final `Reduction` or throws an exception.
    member x.ReductionOrFail() = x.Simple |> Choice.tee2 id failwith


/// A reusable parser created for a specific grammar that can parse input from multiple sources
/// This is the highest-level API. THe parsing function's return values merit some explanation.
/// If parsing succeeds, its return value is the top reduction of the grammar.
/// If it fails, the first message explains the reason it failed.
/// The rest of the messages are a kind of a log of the parser.
type GOLDParser(grammar) =

    let newParser = GOLDParser.CreateParser grammar

    let makeParseResult =
        function
        | Ok (x, messages) -> Success (x, messages)
        | Bad (x :: xs) -> Failure (x, xs)
        | Bad [] -> impossible()

    /// Creates a parser from a `Grammar` stored in an EGT file in the given path.
    /// Trivial reductions are not trimmed.
    /// If there is a problem with the file, the constructor will throw an exception.
    new (egtFile) =
        let grammar = egtFile |> EGT.ofFile |> returnOrFail
        GOLDParser grammar

    /// Creates a parser that parses `input` based on the given `Grammar`, with the option to trim trivial reductions.
    static member CreateParser grammar input = createParser grammar input

    /// Evaluates a `Parser` that parses the given list of characters, unitl it either succeeds or fails.
    /// What it returns is described in the `GOLDParser` class documentation.
    member x.ParseChars input =
        let warn x = warn x ()
        let rec impl p = trial {
            match p with
            | Continuing (msg, x) ->
                do! warn msg
                return! impl x.Value
            | Failed msg ->
                return! fail msg
            | Finished (msg, x) ->
                do! warn msg
                return x
        }
        input |> newParser |> impl |> makeParseResult

    /// Parses a string.
    member x.ParseString input = input |> List.ofString |> x.ParseChars

    /// Parses a `Stream`. Its character encoding is automatically detected.
    member x.ParseStream inputStream =
        inputStream
        |> Seq.ofCharStream false
        |> List.ofSeq
        |> x.ParseChars

    /// Parses the contents of a file in the given path.
    member x.ParseFile path =
        if path |> File.Exists |> not then
            Failure (path |> InputFileNotExist |> ParseMessage.CreateSimple, [])
        else
            use stream = File.OpenRead path
            x.ParseStream stream
