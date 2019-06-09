// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
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
    /// There was an error while the post-processor _was being constructed_.
    | PostProcessorError
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | EGTReadError x -> sprintf "Error while reading the grammar file: %O" x
        | PostProcessorError -> """Error while creating the post-processor.
Some fusers might be missing, or there were type mismatches in the functions of the fusers or the transformers.
Check the post-processor's configuration."""

/// A reusable parser and post-processor, created for a specific grammar, and returning
/// a specific type of object that best describes an expression of the language of this grammar.
[<NoComparison; ReferenceEquality>]
type RuntimeFarkle<'TResult> = private {
    Grammar: Result<Grammar,FarkleError>
    PostProcessor: PostProcessor<'TResult>
}
with
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(grammar, postProcessor) = {Grammar = Ok grammar; PostProcessor = postProcessor}

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(fileName, postProcessor) = {
        Grammar = EGT.ofFile fileName |> Result.mapError EGTReadError
        PostProcessor = postProcessor
    }

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file encoded in Base-64 and <see cref="PostProcessor{TResult}"/>.</summary>
    static member CreateFromBase64String(str, postProcessor) = {
        Grammar = EGT.ofBase64String str |> Result.mapError EGTReadError
        PostProcessor = postProcessor
    }

/// Functions to create and use `RuntimeFarkle`s.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RuntimeFarkle =

    let private createMaybe postProcessor grammar =
        {
            Grammar = grammar
            PostProcessor = postProcessor
        }

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = createMaybe pp rf.Grammar

    /// Creates a `RuntimeFarkle`.
    [<CompiledName("Create")>]
    let create postProcessor (grammar: Grammar) = RuntimeFarkle<_>.Create(grammar, postProcessor)

    /// Creates a `RuntimeFarkle` from the given post-processor, and the .egt file at the given path.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromFile")>]
    let ofEGTFile postProcessor (fileName: string) = RuntimeFarkle<_>.Create(fileName, postProcessor)

    /// Creates a `RuntimeFarkle` from the given post-processor, and the given Base64 representation of an .egt file.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromBase64String")>]
    let ofBase64String postProcessor x = RuntimeFarkle<_>.CreateFromBase64String(x, postProcessor)

    /// Parses and post-processes a `CharStream`.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let parseChars (rf: RuntimeFarkle<'TResult>) fMessage input =
        let fParse grammar =
            let fTransform = CharStreamCallback(fun sym pos data -> rf.PostProcessor.Transform(sym, pos, data))
            let fLALR = LALRParser.LALRStep fMessage grammar rf.PostProcessor
            let fToken pos token =
                token |> Option.map (ParseMessage.TokenRead) |> Option.defaultValue ParseMessage.EndOfInput |> fMessage
                fLALR pos token
            Tokenizer.tokenize fToken [] grammar fTransform input |> Result.mapError ParseError
        rf.Grammar >>= fParse |> Result.map (fun x -> x :?> 'TResult)

    [<CompiledName("ParseMemory")>]
    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    /// This function also accepts a custom parse message handler.
    let parseMemory rf fMessage input = input |> CharStream.ofReadOnlyMemory |> parseChars rf fMessage

    /// Parses and post-processes a string.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseString")>]
    let parseString rf fMessage (inputString: string) = inputString.AsMemory() |> parseMemory rf fMessage

    /// Parses and post-processes a .NET `Stream` with the given character encoding, which may be lazily loaded.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseStream")>]
    let parseStream rf fMessage doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        use sr = new StreamReader(inputStream, encoding)
        use cs =
            match doLazyLoad with
            | true -> CharStream.ofTextReader sr
            | false -> sr.ReadToEnd() |> CharStream.ofString
        parseChars rf fMessage cs

    /// Parses and post-processes a file at the given path with the given character encoding.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseFile")>]
    let parseFile rf fMessage encoding inputFile =
        use s = File.OpenRead(inputFile)
        parseStream rf fMessage true encoding s

    /// Parses and post-processes a string.
    // This function was inspired by FParsec, which has some "runParserOn***" functions,
    // and the simple and easy-to-use function named "run", that just parses a string.
    [<CompiledName("Parse")>]
    let parse rf x = parseString rf ignore x
