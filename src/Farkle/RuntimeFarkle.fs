// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Parser
open Farkle.PostProcessor

/// A type signifying an error during the parsing process.
type FarkleError =
    /// There was a parsing error.
    | ParseError of ParseMessage
    /// There was a post-processing error.
    | PostProcessError of PostProcessError

/// A reusable parser __and post-processor__, created for a specific grammar, and returning
/// a specific object that describes an expression of the language of this grammar.
/// This is the highest-level API, and the easiest-to-use one.
/// 10: BTW, Farkle means: "FARkle Recognizes Known Languages Easily".
/// 20: And "FARkle" means: (GOTO 10) 😁
/// 30: I guess you can't read this line. 😛
type RuntimeFarkle<'TResult> private (grammar: RuntimeGrammar, fPostProcess) =
    let parser = GOLDParser(grammar)

    /// Creates a `RuntimeFarkle`.
    /// The function takes two functions that convert a symbol and a production to another type.
    /// This happens to make the post-processor more convenient to use by converting all the different symbol and production types to type-safe enums.
    static member Create<'TSymbol,'TProduction,'TResult> grammar (fSymbol: _ -> 'TSymbol) (fProduction: _ -> 'TProduction) (postProcessor: PostProcessor<_,_>) =
        let fPostProcess =
            (fSymbol, fProduction)
            ||> AST.ofReductionEx 
            >> postProcessor.PostProcessAST
            >> Result.mapError PostProcessError
        RuntimeFarkle<'TResult>(grammar, fPostProcess)

    member private __.PostProcess (ParseResult (res, msgs)) =
        let result = 
            res
            |> Result.mapError ParseError
            >>= fPostProcess
            |> Result.map (fun x -> x :?> 'TResult)
        result, msgs
    /// Parses and post-processes a `HybridStream` of characters.
    /// Use this method if you want to get a parsing log.
    member x.ParseChars = parser.ParseChars >> x.PostProcess
    /// Parses and post-processes a string.
    member x.ParseString = parser.ParseString >> x.PostProcess >> fst
    /// Parses and post-processes a .NET stream.
    /// This method also takes a configuration object.
    member x.ParseStream = parser.ParseStream >> x.PostProcess >> fst
    /// Parses and post-processes a file at the given path.
    /// This method also takes a configuration object.
    member x.ParseFile = parser.ParseFile >> x.PostProcess >> fst