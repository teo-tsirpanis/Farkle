// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Builder
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.IO
open Farkle.Parser
open Farkle.PostProcessor
open System
open System.IO
open System.Text

/// A type signifying an error during the parsing process.
[<RequireQualifiedAccess>]
type FarkleError =
    /// There was a parsing error.
    | ParseError of Message<ParseErrorType>
    /// There was an error while reading the grammar.
    | EGTReadError of EGTReadError
    /// There was an error while building the grammar .
    | BuildError of BuildErrorType
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | EGTReadError x -> sprintf "Error while reading the grammar file: %O" x
        | BuildError x -> sprintf "Error while building the grammar: %O" x

/// A reusable parser and post-processor, created for a specific grammar, and returning
/// a specific type of object that best describes an expression of the language of this grammar.
[<NoComparison; ReferenceEquality>]
type RuntimeFarkle<'TResult> = private {
    Grammar: Result<Grammar,FarkleError>
    OptimizedOperations: OptimizedOperations
    PostProcessor: PostProcessor<'TResult>
}
with
    static member internal CreateMaybe postProcessor grammarMaybe =
        let oops =
            match grammarMaybe with
            | Ok grammar -> OptimizedOperations.optimized grammar
            | Error _ -> Unchecked.defaultof<_>
        {
            Grammar = grammarMaybe
            OptimizedOperations = oops
            PostProcessor = postProcessor
        }
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(grammar, postProcessor) =
        grammar
        |> Ok
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(fileName, postProcessor) =
        fileName
        |> EGT.ofFile
        |> Result.mapError FarkleError.EGTReadError
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file encoded in Base64 and <see cref="PostProcessor{TResult}"/>.</summary>
    static member CreateFromBase64String(str, postProcessor) =
        str
        |> EGT.ofBase64String
        |> Result.mapError FarkleError.EGTReadError
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// <summary>Gets the grammar behind the <see cref="RuntimeFarkle{TResult}"/>.</summary>
    member this.TryGetGrammar() = this.Grammar

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = {
        Grammar = rf.Grammar
        OptimizedOperations = rf.OptimizedOperations
        PostProcessor = pp
    }

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

    /// Creates a `RuntimeFarkle` from the given `DesigntimeFarkle<T>`.
    /// In case there is a problem with the grammar, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("Build")>]
    let build df =
        let theFabledGrammar, theTriumphantPostProcessor = DesigntimeFarkleBuild.build df
        theFabledGrammar
        |> Result.mapError FarkleError.BuildError
        |> RuntimeFarkle<_>.CreateMaybe theTriumphantPostProcessor

    /// Parses and post-processes a `CharStream`.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let parseChars (rf: RuntimeFarkle<'TResult>) fMessage input =
        match rf.Grammar with
        | Ok grammar ->
            let fTransform = CharStreamCallback(fun sym pos data -> rf.PostProcessor.Transform(sym, pos, data))
            let fTokenize input = Tokenizer.tokenize grammar.Groups grammar.DFAStates rf.OptimizedOperations fTransform fMessage input
            try
                LALRParser.parseLALR fMessage grammar.LALRStates rf.OptimizedOperations rf.PostProcessor fTokenize input :?> 'TResult |> Ok
            with
            | ParseError msg -> msg |> FarkleError.ParseError |> Error
        | Error x -> Error x

    [<CompiledName("ParseMemory")>]
    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    /// This function also accepts a custom parse message handler.
    let parseMemory rf fMessage input = input |> CharStream.ofReadOnlyMemory |> parseChars rf fMessage

    /// Parses and post-processes a string.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseString")>]
    let parseString rf fMessage inputString = inputString |> CharStream.ofString |> parseChars rf fMessage

    /// Parses and post-processes a .NET `Stream` with the given character encoding, which may be lazily loaded.
    /// The stream is disposed after its contents are parsed, which is why you might want to parse a text reader.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseStream"); Obsolete("Directly parsing streams opens questions like whether to be disposed by Farkle or not. Use parseTextReader or parseFile instead.")>]
    let parseStream rf fMessage doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        use sr = new StreamReader(inputStream, encoding)
        use cs =
            match doLazyLoad with
            | true -> CharStream.ofTextReader sr
            | false -> sr.ReadToEnd() |> CharStream.ofString
        parseChars rf fMessage cs

    let parseTextReader rf fMessage textReader =
        let cs = CharStream.ofTextReader textReader
        parseChars rf fMessage cs

    /// Parses and post-processes a file at the given path with the given character encoding.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseFile")>]
    let parseFile rf fMessage inputFile =
        use s = File.OpenText(inputFile)
        parseTextReader rf fMessage s

    /// Parses and post-processes a string.
    // This function was inspired by FParsec, which has some "runParserOn***" functions,
    // and the simple and easy-to-use function named "run", that just parses a string.
    [<CompiledName("Parse")>]
    let parse rf x = parseString rf ignore x
