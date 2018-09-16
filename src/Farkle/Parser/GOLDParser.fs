// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open Farkle.Monads

/// Functions to create `AST`s by parsing input, based on `RuntimeGrammar`s.
/// They accept a callback for each log message the parser encounters.
[<RequireQualifiedAccess>]
module GOLDParser =

    open State

    let private tokenize = state {
        let! tokenizer = getOptic ParserState.TheTokenizer_
        match tokenizer with
        | EndlessProcess (ExtraTopLevelOperators.Lazy (x, xs)) ->
            do! get >>= (fun s -> put {s with TheTokenizer = xs; CurrentPosition = x.CurrentPosition; IsGroupStackEmpty = x.IsGroupStackEmpty})
            return x.NewToken
    }

    let private parseLALR token = state {
        let! lalrParser = getOptic ParserState.TheLALRParser_ <!> (fun (LALRParser x) -> x)
        let result, newParser = lalrParser token
        do! setOptic ParserState.TheLALRParser_ newParser
        return result
    }

    /// Parses a `HybridStream` of characters. 
    let parseChars grammar fMessage input =
        let fMessage (state: ParserState) x = (state.CurrentPosition, x) |> ParseMessage |> fMessage
        let fail (state: ParserState) x = (state.CurrentPosition, x) |> ParseError.ParseError |> Result.Error
        let rec impl (state: ParserState) =
            // The following line must be there.
            // We want to know whether the group stack was empty before the tokenizing.
            let isGroupStackEmpty = state.IsGroupStackEmpty
            match state.NextToken with
            | None ->
                let newToken, state = tokenize state
                if newToken.Symbol = EndOfFile && not isGroupStackEmpty then
                    fail state GroupError
                else
                    fMessage state <| TokenRead newToken
                    impl {state with NextToken = Some newToken}
            | Some nextToken ->
                match nextToken.Symbol with
                | Noise _ ->
                    impl {state with NextToken = None}
                | Error -> fail state <| LexicalError nextToken.Data.[0]
                | EndOfFile when not isGroupStackEmpty -> fail state GroupError
                | _ ->
                    let lalrResult, state = run (parseLALR nextToken) state
                    match lalrResult with
                    | LALRResult.Accept x -> Ok x
                    | LALRResult.Shift x ->
                        fMessage state <| ParseMessageType.Shift x
                        impl {state with NextToken = None}
                    | LALRResult.Reduce x ->
                        fMessage state <| Reduction x
                        impl state
                    | LALRResult.SyntaxError (x, y) -> fail state <| SyntaxError (x, y)
                    | LALRResult.InternalError x -> fail state <| InternalError x
        let dfa = RuntimeGrammar.dfaStates grammar
        let groups = RuntimeGrammar.groups grammar
        let lalr = RuntimeGrammar.lalrStates grammar
        let state = ParserState.create (Tokenizer.create dfa groups input) (LALRParser.create lalr)
        impl state

    /// Parses a string.
    let parseString g fMessage (input: string) = input |> HybridStream.ofSeq false |> parseChars g fMessage

    /// Parses a .NET `Stream` whose bytes are encoded with the given `Encoding`.
    /// There is an option to load the entire stream at once, instead of gradually loading it the moment it is required.
    /// It slightly improves performance, but it should not be used on files whose size might be big.
    let parseStream g fMessage doLazyLoad encoding inputStream =
        inputStream
        |> Seq.ofCharStream false encoding
        |> HybridStream.ofSeq doLazyLoad
        |> parseChars g fMessage

    /// Parses the contents of a file in the given path whose bytes are encoded with the given `Encoding`.
    let parseFile g fMessage doLazyLoad encoding path =
        use stream = System.IO.File.OpenRead path
        parseStream g fMessage doLazyLoad encoding stream
