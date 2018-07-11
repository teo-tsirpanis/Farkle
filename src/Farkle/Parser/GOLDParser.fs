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
/// It contains the result and a list of log messages describing the parsing process in detail.
/// The result is either the final `Reduction`, or the fatal `ParseMessage`
/// In this case, it is not included in the previously mentioned log message list.
type ParseResult = ParseResult of Result<Reduction, ParseMessage> * ParseMessage list
with
    /// The content of this type, unwrapped.
    static member Value x = match x with | ParseResult (x, y) -> x, y
    /// The parsing log. In case of a failure, the fatal message is _not_ included.
    member x.Messages = x |> ParseResult.Value |> snd
    /// The parsing log in human-friendly format. In case of a failure, the fatal message is _not_ included.
    member x.MessagesAsString = x.Messages |> List.map string
    /// A simple `Choice` with either the final `Reduction` or the fatal `ParseMessage` as a string.
    member x.Simple = x |> ParseResult.Value |> fst |> Trial.tee Choice1Of2 (string >> Choice2Of2)
    /// Returns the final `Reduction` or throws an exception.
    member x.ResultOrFail() = x.Simple |> Choice.tee2 id failwith

/// A set of settings to customize a parser.
type GOLDParserConfig = {
    /// The text encoding that will be used.
    Encoding: Encoding
    /// Whether the input stream will be lazily loaded.
    LazyLoad: bool
}
with
    /// The default configuration. UTF-8 encoding and lazy loading.
    static member Default = {Encoding = Encoding.UTF8; LazyLoad = true}
    member x.WithLazyLoad v = {x with LazyLoad = v}
    member x.WithEncoding v = {x with Encoding = v}


/// A reusable parser created for a specific grammar that can parse input from multiple sources.
type GOLDParser(grammar) =

    let newParser = GOLDParser.CreateParser grammar

    let makeParseResult res =
        match run res [] with
        | res, msgs -> ParseResult(res, List.rev msgs)

    /// Creates a parser from a `Grammar` stored in an EGT file in the given path.
    /// Trivial reductions are not trimmed.
    /// If there is a problem with the file, the constructor will throw an exception.
    new (egtFile) =
        let grammar = egtFile |> EGT.ofFile |> returnOrFail |> RuntimeGrammar.ofGOLDGrammar
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

    /// Parses a .NET `Stream`.
    /// A `GOLDParserConfig` is required.
    member x.ParseStream (inputStream, settings) =
        inputStream
        |> Seq.ofCharStream false settings.Encoding
        |> (if settings.LazyLoad then LazyList.ofSeq >> Lazy else List.ofSeq >> Eager)
        |> x.ParseChars

    /// Parses a .NET `Stream`.
    member x.ParseStream inputStream = x.ParseStream(inputStream, GOLDParserConfig.Default)

    /// Parses the contents of a file in the given path.
    /// A `GOLDParserConfig` is required.
    member x.ParseFile (path, settings) =
        if path |> File.Exists |> not then
            ParseResult (path |> InputFileNotExist |> ParseMessage.CreateSimple |> Result.Error, [])
        else
            use stream = File.OpenRead path
            x.ParseStream(stream, settings)

    member x.ParseFile path = x.ParseFile(path, GOLDParserConfig.Default)
