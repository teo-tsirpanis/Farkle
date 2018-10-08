// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar
open Farkle.Monads
open Farkle.PostProcessor

/// Functions to create `AST`s by parsing input, based on `RuntimeGrammar`s.
/// They accept a callback for each log message the parser encounters.
[<RequireQualifiedAccess>]
module internal GOLDParser =

    open State

    let private parseLALR token: State<_,_> = fun state -> state |> (fun (LALRParser x) -> x) <| token

    /// Parses a `HybridStream` of characters. 
    let parseChars (grammar: #RuntimeGrammar) (pp: PostProcessor<'result>) fMessage input =
        let fMessage = curry (ParseMessage >> fMessage)
        let fail = curry (ParseError.ParseError >> Error >> Some)
        let impl (x: TokenizerResult): State<_,_> = fun state ->
            let pos = x.Position
            let fail = fail pos
            let fMessage = fMessage pos
            match x with
            | TokenizerResult.GroupError _ -> fail GroupError, state
            | TokenizerResult.LexicalError (x, _) -> fail <| LexicalError x, state
            | TokenizerResult.TokenRead {Symbol = Noise _} -> None, state
            | TokenizerResult.TokenRead newToken ->
                fMessage <| TokenRead newToken
                let rec lalrLoop state =
                    let lalrResult, state = run (parseLALR newToken) state
                    match lalrResult with
                    | LALRResult.Accept x -> Some <| Ok (x :?> 'result), state
                    | LALRResult.Shift x ->
                        fMessage <| ParseMessageType.Shift x
                        None, state
                    | LALRResult.Reduce x ->
                        fMessage <| Reduction x
                        lalrLoop state
                    | LALRResult.SyntaxError (x, y) -> fail <| SyntaxError (x, y), state
                    | LALRResult.InternalError x -> fail <| InternalError x, state
                lalrLoop state
        let tokenizer = Tokenizer.create grammar.DFA grammar.Groups input
        let state = LALRParser.create grammar.LALR pp
        State.run (Extra.State.runOverSeq tokenizer impl) state |> fst
