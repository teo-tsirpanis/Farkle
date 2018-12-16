// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.Parser
open Farkle.PostProcessor
open System
open System.IO
open System.Text

/// A type signifying an error during the parsing process.
type FarkleError =
    /// There was a parsing error.
    | ParseError of Message<ParseErrorType>
    /// There was an error while reading the grammar.
    | EGTReadError of EGTReadError
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | EGTReadError x -> sprintf "Error while reading the grammar file: %O" x

/// A reusable parser __and post-processor__, created for a specific grammar, and returning
/// a specific object that best describes an expression of the language of this grammar.
/// This is the highest-level API, and the easiest-to-use one.
/// 10: BTW, Farkle means: "FArkle Recognizes Known Languages Easily".
/// 20: And "FArkle" means: (GOTO 10) 😁
/// 30: I guess you can't read this line. 😛
[<NoComparison; NoEquality>]
type RuntimeFarkle<'TResult> = private {
    Grammar: Result<Grammar,FarkleError>
    PostProcessor: PostProcessor<'TResult>
}

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    /// Returns the `GOLDParser` within the `RuntimeFarkle`.
    /// This function is useful to access the lower-level APIs, for more advanced cases of parsing.
    let internal grammar {Grammar = x} = x

    /// Returns the `PostProcessor` within the `RuntimeFarkle`.
    let internal postProcessor {PostProcessor = x} = x

    let private createMaybe postProcessor grammar =
        {
            Grammar = grammar
            PostProcessor = postProcessor
        }

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = {Grammar = rf.Grammar; PostProcessor = pp}

    /// Creates a `RuntimeFarkle`.
    [<CompiledName("Create")>]
    let create postProcessor grammar = createMaybe postProcessor (Ok grammar)

    /// Creates a `RuntimeFarkle` from the GOLD Parser grammar file that is located at the given path.
    /// Other than that, this function works just like its `RuntimeGrammar` counterpart.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromEGTFile")>]
    let ofEGTFile postProcessor fileName  =
        fileName
        |> EGT.ofFile
        |> Result.mapError EGTReadError
        |> createMaybe postProcessor

    /// Creates a `RuntimeFarkle` from the GOLD Parser grammar file that is located at the given path.
    /// Other than that, this function works just like its `RuntimeGrammar` counterpart.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromBase64String")>]
    let ofBase64String postProcessor x =
        x
        |> EGT.ofBase64String
        |> Result.mapError EGTReadError
        |> createMaybe postProcessor

    /// Parses and post-processes a `CharStream` of characters.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let private parseChars {Grammar = g; PostProcessor = pp} fMessage input =
        let fParse grammar pp fMessage input =
            let fLALR = LALRParser.LALRStep fMessage grammar pp
            let fToken pos token =
                fMessage <| Message(pos, ParseMessageType.TokenRead token)
                fLALR pos token
            Tokenizer.tokenize Error fToken [] grammar pp input
        g >>= (fun g -> fParse g pp fMessage input |> Result.mapError ParseError)

    /// Parses and post-processes a string.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseString")>]
    let parseString rf fMessage (inputString: string) = inputString.AsMemory() |> CharStream.ofReadOnlyMemory |> parseChars rf fMessage

    /// Parses and post-processes a .NET `Stream` with the given character encoding, which may be lazily loaded.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseStream")>]
    let parseStream rf fMessage doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        use sr = new StreamReader(inputStream, encoding)
        sr.ReadToEnd() |> parseString rf fMessage

    /// Parses and post-processes a file at the given path with the given settings that are explained in the `GOLDParser` module.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseFile")>]
    let parseFile rf fMessage doLazyLoad encoding inputFile =
        File.ReadAllText(inputFile, encoding) |> parseString rf fMessage

    /// Parses and post-processes a string.
    // This function was inspired by FParsec, which has some "runParserOn***" functions,
    // and the simple and easy-to-use function named "run", that just parses a string.
    [<CompiledName("Parse")>]
    let parse rf x = parseString rf ignore x
