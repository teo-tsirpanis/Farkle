// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Monads.StateResult
open Farkle.Grammar
open Farkle.Parser
open Farkle.Parser.Internal
open FSharpx.Collections
open System.IO
open System.Text

/// A dedicated type to signify the result of a parser.
type ParseResult =
    /// Parsing succeeded. The final `Reduction` and the parsing log are returned.
    | Success of reduction: Reduction * messages: ParseMessage list
    /// Parsing failed, The fatal `ParseMessage` is separately returned.
    | Failure of fatalMessage: ParseMessage * messages: ParseMessage list
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

/// A set of settings to customize a parser.
type GOLDParserConfig = {
    /// The text encoding that will be used.
    Encoding: Encoding
    /// Whether the input stream will be lazily loaded.
    LazyLoad: bool
}
with
    /// The default configuration.
    static member Default = {Encoding = Encoding.UTF8; LazyLoad = true}


/// A reusable parser created for a specific grammar that can parse input from multiple sources
/// This is the highest-level API. THe parsing function's return values merit some explanation.
/// If parsing succeeds, its return value is the top reduction of the grammar.
/// If it fails, the first message explains the reason it failed.
/// The rest of the messages are a kind of a log of the parser.
type GOLDParser(grammar) =

    let newParser = GOLDParser.CreateParser grammar

    let makeParseResult res =
        match run res [] with
        | Ok red, msgs -> Success(red, List.rev msgs)
        | Result.Error x, msgs -> Failure (x, List.rev msgs)

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
    member __.ParseChars input =
        let warn x = sresult {
            let! xs = get
            do! put <| x :: xs
        }
        let rec impl p = sresult {
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
    member x.ParseString input = input |> List.ofString |> Eager |> x.ParseChars

    /// Parses a `Stream`.
    member x.ParseStream (inputStream, settings) =
        inputStream
        |> Seq.ofCharStream false settings.Encoding
        |> (if settings.LazyLoad then LazyList.ofSeq >> Lazy else List.ofSeq >> Eager)
        |> x.ParseChars

    /// Parses the contents of a file in the given path.
    member x.ParseFile (path, settings) =
        if path |> File.Exists |> not then
            Failure (path |> InputFileNotExist |> ParseMessage.CreateSimple, [])
        else
            use stream = File.OpenRead path
            x.ParseStream(stream, settings)
