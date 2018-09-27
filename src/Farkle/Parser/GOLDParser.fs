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

    let private parseLALR token: State<_,_> = fun state -> state |> (fun (LALRParser x) -> x) <| token

    /// Parses a `HybridStream` of characters. 
    let parseChars (grammar: #RuntimeGrammar) fMessage input =
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
                    | LALRResult.Accept x -> Some <| Ok x, state
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
        let state = LALRParser.create grammar.LALR
        State.run (Extra.State.runOverSeq tokenizer impl) state |> fst

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
