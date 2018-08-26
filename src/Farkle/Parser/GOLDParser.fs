// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar.GOLDParser
open FSharpx.Collections
open System.IO
open System.Text

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
type GOLDParser = private {
    Grammar: RuntimeGrammar
}

module GOLDParser =

    let asGrammar {Grammar = grammar} = grammar

    let ofRuntimeGrammar grammar = {Grammar = grammar}

    /// Creates a parser from a `Grammar` stored in an EGT file in the given path.
    /// If there is a problem with the file, the constructor will throw an exception.
    let ofEGTFile egtFile = egtFile |> EGT.ofFile |> returnOrFail |> ofRuntimeGrammar

    /// Evaluates a `Parser` that parses the given list of characters, unitl it either succeeds or fails.
    /// A custom function that accepts the parse messages is required.
    /// What it returns is described in the `GOLDParser` class documentation.
    let parseChars {Grammar = grammar} fMessage input =
        let rec impl p = either {
            match p with
            | Continuing (msg, x) ->
                do fMessage msg
                return! impl x.Value
            | Failed msg ->
                return! fail msg
            | Finished (msg, x) ->
                do fMessage msg
                return x
        }
        input |> Parser.create grammar |> impl

    /// Parses a string.
    /// A custom function that accepts the parse messages is required.
    let parseString gp fMessage input = input |> HybridStream.ofSeq false |> parseChars gp fMessage

    /// Parses a .NET `Stream`.
    /// A custom function that accepts the parse messages is required.
    /// A `GOLDParserConfig` is required as well.
    let parseStream gp fMessage settings inputStream =
        inputStream
        |> Seq.ofCharStream false settings.Encoding
        |> HybridStream.ofSeq settings.LazyLoad
        |> parseChars gp fMessage

    /// Parses the contents of a file in the given path.
    /// A custom function that accepts the parse messages is required.
    /// A `GOLDParserConfig` is required as well.
    let parseFile gp fMessage settings path =
        if path |> File.Exists |> not then
            path |> InputFileNotExist |> ParseMessage.CreateSimple |> Result.Error
        else
            use stream = File.OpenRead path
            parseStream gp fMessage settings stream
