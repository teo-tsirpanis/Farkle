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

    let private parseLALR token = state {
        let! lalrParser = get <!> (fun (LALRParser x) -> x)
        let result, newParser = lalrParser token
        do! put newParser
        return result
    }

    /// Parses a `HybridStream` of characters. 
    let parseChars grammar fMessage input =
        let fMessage = curry (ParseMessage >> fMessage)
        let fail = curry (ParseError.ParseError >> Result.Error >> Some)
        let impl {NewToken = newToken; CurrentPosition = pos; IsGroupStackEmpty = isGroupStackEmpty}: State<_,_> = fun (state: ParserState) ->
            fMessage pos <| TokenRead newToken
            match newToken.Symbol with
            | Noise _ -> None, state
            | Error -> fail pos <| LexicalError newToken.Data.[0], state
            | EndOfFile when not isGroupStackEmpty -> fail pos GroupError, state
            | _ ->
                let rec lalrLoop state =
                    let lalrResult, state = run (parseLALR newToken) state
                    match lalrResult with
                    | LALRResult.Accept x -> Some <| Ok x, state
                    | LALRResult.Shift x ->
                        fMessage pos <| ParseMessageType.Shift x
                        None, state
                    | LALRResult.Reduce x ->
                        fMessage pos <| Reduction x
                        lalrLoop state
                    | LALRResult.SyntaxError (x, y) -> fail pos <| SyntaxError (x, y), state
                    | LALRResult.InternalError x -> fail pos <| InternalError x, state
                lalrLoop state
        let dfa = RuntimeGrammar.dfaStates grammar
        let groups = RuntimeGrammar.groups grammar
        let lalr = RuntimeGrammar.lalrStates grammar
        let tokens = Tokenizer.create dfa groups input
        let state = LALRParser.create lalr
        State.eval (EndlessProcess.runOver impl tokens) state

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
