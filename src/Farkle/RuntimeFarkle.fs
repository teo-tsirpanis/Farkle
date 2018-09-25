// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammar.GOLDParser
open Farkle.Parser
open Farkle.PostProcessor

/// A type signifying an error during the parsing process.
type FarkleError =
    /// There was a parsing error.
    | ParseError of ParseError
    /// There was a post-processing error.
    | PostProcessError of PostProcessError
    /// There was an error while reading the grammar.
    | EGTReadError of EGTReadError
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | PostProcessError x -> sprintf "Post-processing error: %O" x
        | EGTReadError x -> sprintf "Error while reading the grammar file: %O" x

/// A reusable parser __and post-processor__, created for a specific grammar, and returning
/// a specific object that describes an expression of the language of this grammar.
/// This is the highest-level API, and the easiest-to-use one.
/// 10: BTW, Farkle means: "FArkle Recognizes Known Languages Easily".
/// 20: And "FArkle" means: (GOTO 10) 😁
/// 30: I guess you can't read this line. 😛
[<NoComparison; NoEquality>]
type RuntimeFarkle<'TResult> = private {
    Parser: Result<RuntimeGrammar,FarkleError>
    PostProcessor: PostProcessor
}

module RuntimeFarkle =

    /// Returns the `GOLDParser` within the `RuntimeFarkle`.
    /// This function is useful to access the lower-level APIs, for more advanced cases of parsing.
    let parser {Parser = x} = x

    /// Returns the `PostProcessor` within the `RuntimeFarkle`.
    let postProcessor {PostProcessor = x} = x

    /// Creates a `RuntimeFarkle`.
    /// The function takes a `RuntimeGrammar` and a `PostProcessor` that might have failed.
    [<CompiledName("Create")>]
    let create<'TResult> grammar postProcessor: RuntimeFarkle<'TResult> =
        {
            Parser = Ok grammar
            PostProcessor = postProcessor
        }

    /// Creates a `RuntimeFarkle` from the GOLD Parser grammar file that is located at the given path.
    /// Other than that, this function works just like its `RuntimeGrammar` counterpart.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromEGTFile")>]
    let ofEGTFile<'TResult> fileName postProcessor: RuntimeFarkle<'TResult> =
        fileName
        |> EGT.ofFile
        |> Result.mapError (EGTReadError)
        |> tee
            (flip create postProcessor)
            (fun err -> {Parser = Error err; PostProcessor = postProcessor})

    let private postProcess (rf: RuntimeFarkle<'TResult>) res =
        res
        |> Result.mapError ParseError
        >>= (PostProcessor.postProcessAST (postProcessor rf) >> Result.mapError PostProcessError)
        |> Result.map (fun x -> x :?> 'TResult)

    /// Parses and post-processes a `HybridStream` of characters.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let parseChars fMessage x input =
        x |> parser >>= (fun g -> GOLDParser.parseChars g fMessage input |> postProcess x)

    /// Parses and post-processes a string.
    [<CompiledName("ParseString")>]
    let parseString x inputString = x |> parser >>= (fun g -> GOLDParser.parseString g ignore inputString |> postProcess x)

    /// Parses and post-processes a file at the given path with the given settings that are explained in the `GOLDParser` module.
    [<CompiledName("ParseFile")>]
    let parseFile x doLazyLoad encoding inputFile =
        x |> parser >>= (fun g -> GOLDParser.parseFile g ignore doLazyLoad encoding inputFile |> postProcess x)

    /// Parses and post-processes a .NET `Stream` with the given settings that are explained in the `GOLDParser` module.
    [<CompiledName("ParseStream")>]
    let parseStream x doLazyLoad encoding inputStream =
        x |> parser >>= (fun g -> GOLDParser.parseStream g ignore doLazyLoad encoding inputStream |> postProcess x)
