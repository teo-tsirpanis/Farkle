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
    | ParseError of ParseMessage
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
// `fPostProcess` is hiding away the post-processor's two generic types.
type RuntimeFarkle<'TResult> = private {
    Parser: Result<GOLDParser,FarkleError>
    PostProcessor: PostProcessor
}

module RuntimeFarkle =

    let internal parser {Parser = x} = x

    let internal postProcessor {PostProcessor = x} = x

    /// Creates a `RuntimeFarkle`.
    /// The function takes a `RuntimeGrammar`, two functions that convert a symbol and a production to another type, and a `PostProcessor` that might have failed.
    /// If the post-processing has failed, the `RuntimeFarkle` will fail every time it is used.
    /// This happens to make the post-processor more convenient to use by converting all the different symbol and production types to type-safe enums.
    let create<'TResult> (grammar: RuntimeGrammar) postProcessor: RuntimeFarkle<'TResult> =
        {
            Parser = grammar |> GOLDParser |> Ok
            PostProcessor = postProcessor
        }

    /// Creates a `RuntimeFarkle` from the GOLD Parser grammar file that is located at the given path.
    /// Other than that, this function works just like its `RuntimeGrammar` counterpart.
    /// Also, in case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    let ofEGTFile<'TResult> fileName postProcessor: RuntimeFarkle<'TResult> =
        fileName
        |> EGT.ofFile
        |> Result.mapError (EGTReadError)
        |> tee
            (flip create postProcessor)
            (fun err -> {Parser = fail err; PostProcessor = postProcessor})

    let private postProcess (rf: RuntimeFarkle<'TResult>) (ParseResult (res, msgs)) =
        let result =
            res
            |> Result.mapError ParseError
            >>= (AST.ofReduction >> PostProcessor.postProcessAST (postProcessor rf) >> Result.mapError PostProcessError)
            |> Result.map (fun x -> x :?> 'TResult)
        result, msgs

    /// Parses and post-processes a `HybridStream` of characters.
    /// Use this method if you want to get a parsing log.
    let parseChars x input =
        x |> parser |> tee (fun gp -> gp.ParseChars input |> postProcess x) (fun err -> fail err, [])

    /// Parses and post-processes a string.
    let parseString x inputString = x |> parser >>= (fun p -> p.ParseString inputString |> postProcess x |> fst)

    /// Parses and post-processes a file at the given path with the given settings.
    let parseFile x settings inputFile = x |> parser >>= (fun p -> p.ParseFile (inputFile, settings) |> postProcess x |> fst)

    /// Parses and post-processes a .NET `Stream` with the given settings.
    let parseStream x settings inputStream = x |> parser >>= (fun p -> p.ParseStream (inputStream, settings) |> postProcess x |> fst)
