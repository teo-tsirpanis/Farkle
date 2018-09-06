// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open Farkle.Monads

/// Functions to create low-level `Parser`s.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Parser =

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

    /// Creates an `AST` from the given `HybridStream` based on the given `RuntimeGrammar`.
    let private stepParser grammar fMessage input =
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
