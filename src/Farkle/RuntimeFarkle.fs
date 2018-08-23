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
type RuntimeFarkle<'TResult> private (parser, fPostProcess) =

    /// Creates a `RuntimeFarkle`.
    /// The function takes a `RuntimeGrammar`, two functions that convert a symbol and a production to another type, and a `PostProcessor` that might have failed.
    /// If the post-processing has failed, the `RuntimeFarkle` will fail every time it is used.
    /// This happens to make the post-processor more convenient to use by converting all the different symbol and production types to type-safe enums.
    static member Create<'TSymbol,'TProduction,'TResult>
        (grammar: RuntimeGrammar) (fSymbol: _ -> 'TSymbol) (fProduction: _ -> 'TProduction) (postProcessor: PostProcessor<_,_>) =
        let fPostProcess x =
            (fSymbol, fProduction, x)
            |||> AST.ofReductionEx 
            |> postProcessor.PostProcessAST
            |> Result.mapError PostProcessError
        RuntimeFarkle<'TResult>(grammar |> GOLDParser |> Ok, fPostProcess)

    /// Creates a `RuntimeFarkle` from the GOLD Parser grammar file that is located at the given path.
    /// Other than that, this function works just like its `RuntimeGrammar` counterpart.
    /// Also, in case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    static member CreateFromFile<'TSymbol,'TProduction,'TResult> fileName fSymbol fProduction postProcessor =
        fileName
        |> EGT.ofFile
        |> Result.mapError (EGTReadError)
        |> tee
            (fun g -> RuntimeFarkle.Create<'TSymbol,'TProduction,'TResult> g fSymbol fProduction postProcessor)
            (fun err -> RuntimeFarkle(fail err, fun _ -> fail err))

    member private __.PostProcess (ParseResult (res, msgs)) =
        let result = 
            res
            |> Result.mapError ParseError
            >>= fPostProcess
            |> Result.map (fun x -> x :?> 'TResult)
        result, msgs
    /// Parses and post-processes a `HybridStream` of characters.
    /// Use this method if you want to get a parsing log.
    member x.ParseChars input = parser |> tee (fun p -> p.ParseChars input |> x.PostProcess) (fun err -> fail err, [])
    /// Parses and post-processes a string.
    member x.ParseString inputString = parser >>= (fun p -> p.ParseString inputString |> x.PostProcess |> fst)
    /// Parses and post-processes a file at the given path.
    member x.ParseFile inputFile = parser >>= (fun p -> p.ParseFile inputFile |> x.PostProcess |> fst)
    /// Parses and post-processes a file at the given path.
    /// This method also takes a configuration object.
    member x.ParseFile (inputFile, settings) = parser >>= (fun p -> p.ParseFile (inputFile, settings) |> x.PostProcess |> fst)
    /// Parses and post-processes a .NET `Stream`.
    member x.ParseStream inputStream = parser >>= (fun p -> p.ParseStream inputStream |> x.PostProcess |> fst)
    /// Parses and post-processes a .NET `Stream`.
    /// This method also takes a configuration object.
    member x.ParseStream (inputStream, settings) = parser >>= (fun p -> p.ParseStream (inputStream, settings) |> x.PostProcess |> fst)