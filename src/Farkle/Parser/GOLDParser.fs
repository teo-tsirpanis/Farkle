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

    open StateResult

    let private tokenize = sresult {
        let! tokenizer = getOptic ParserState.TheTokenizer_
        match tokenizer with
        | EndlessProcess (ExtraTopLevelOperators.Lazy (x, xs)) ->
            do! get >>= (fun s -> put {s with TheTokenizer = xs; CurrentPosition = x.CurrentPosition; IsGroupStackEmpty = x.IsGroupStackEmpty})
            return x.NewToken
    }

    let private parseLALR token = sresult {
        let! lalrParser = getOptic ParserState.TheLALRParser_ <!> (fun (LALRParser x) -> x)
        let result, newParser = lalrParser token
        do! setOptic ParserState.TheLALRParser_ newParser
        return result
    }

    /// Parses a `HybridStream` of characters. 
    let parseChars grammar fMessage input =
        let fMessage messageType = getOptic ParserState.CurrentPosition_ <!> (yrruc ParseMessage messageType >> fMessage)
        let rec impl() = sresult {
            let! tokens = getOptic ParserState.InputStack_
            let! isGroupStackEmpty = getOptic ParserState.IsGroupStackEmpty_
            match tokens with
            | [] ->
                let! newToken = tokenize
                do! setOptic ParserState.InputStack_ [newToken]
                if newToken.Symbol = EndOfFile && not isGroupStackEmpty then
                    return! fail GroupError
                else
                    do! fMessage <| TokenRead newToken
                    return! impl()
            | newToken :: xs ->
                match newToken.Symbol with
                | Noise _ ->
                    do! setOptic ParserState.InputStack_ xs
                    return! impl()
                | Error -> return! fail <| LexicalError newToken.Data.[0]
                | EndOfFile when not isGroupStackEmpty -> return! fail GroupError
                | _ ->
                    let! lalrResult = parseLALR newToken
                    match lalrResult with
                    | LALRResult.Accept x -> return x
                    | LALRResult.Shift x ->
                        do! mapOptic ParserState.InputStack_ List.skipLast
                        do! fMessage <| ParseMessageType.Shift x
                        return! impl()
                    | ReduceNormal x ->
                        do! fMessage <| Reduction x
                        return! impl()
                    | LALRResult.SyntaxError (x, y) -> return! fail <| SyntaxError (x, y)
                    | LALRResult.InternalError x -> return! fail <| InternalError x
        }
        let dfa = RuntimeGrammar.dfaStates grammar
        let groups = RuntimeGrammar.groups grammar
        let lalr = RuntimeGrammar.lalrStates grammar
        let state = ParserState.create (Tokenizer.create dfa groups input) (LALRParser.create lalr)
        let (result, nextState) = run (impl()) state
        result |> Result.mapError (curry ParseError nextState.CurrentPosition)

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
