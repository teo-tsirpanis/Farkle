// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open Farkle.Monads

module internal Internal =

    open State

    let tokenize = state {
        let! tokenizer = getOptic ParserState.TheTokenizer_
        match tokenizer.Value with
        | EndlessProcess (x, xs) ->
            do! setOptic ParserState.TheTokenizer_ xs
            do! setOptic ParserState.CurrentPosition_ x.CurrentPosition
            do! setOptic ParserState.IsGroupStackEmpty_ x.IsGroupStackEmpty
            return x.NewToken
    }

    let parseLALR token = state {
        let! lalrParser = getOptic ParserState.TheLALRParser_ <!> (fun (LALRParser x) -> x)
        let result, newParser = lalrParser token
        do! setOptic ParserState.TheLALRParser_ newParser
        return result
    }

    let rec stepParser p =
        let rec impl() = state {
            let! tokens = getOptic ParserState.InputStack_
            let! isGroupStackEmpty = getOptic ParserState.IsGroupStackEmpty_
            match tokens with
            | [] ->
                let! newToken = tokenize
                do! setOptic ParserState.InputStack_ [newToken]
                if newToken.Symbol = EndOfFile && not isGroupStackEmpty then
                    return GroupError
                else
                    return TokenRead newToken
            | newToken :: xs ->
                match newToken.Symbol with
                | Noise _ ->
                    do! setOptic ParserState.InputStack_ xs
                    return! impl()
                | Error -> return LexicalError newToken.Data.[0]
                | EndOfFile when not isGroupStackEmpty -> return GroupError
                | _ ->
                    let! lalrResult = parseLALR newToken
                    match lalrResult with
                    | LALRResult.Accept x -> return ParseMessageType.Accept x
                    | LALRResult.Shift x ->
                        do! mapOptic ParserState.InputStack_ List.skipLast
                        return ParseMessageType.Shift x
                    | ReduceNormal x -> return Reduction x
                    | LALRResult.SyntaxError (x, y) -> return SyntaxError (x, y)
                    | LALRResult.InternalError x -> return InternalError x
        }
        let (result, nextState) = run (impl()) p
        let makeMessage = nextState.CurrentPosition |> ParseMessage.Create
        match result with
        | ParseMessageType.Accept x -> Parser.Finished (x |> ParseMessageType.Accept |> makeMessage, x)
        | x when x.IsError -> x |> makeMessage |> Parser.Failed
        | x -> Parser.Continuing (makeMessage x, lazy (stepParser nextState))

    let createParser (grammar: Grammar) input =
        let state = ParserState.create (TokenizerImpl.create grammar.DFA grammar.Groups input) (LALRParser.create grammar.LALR)
        stepParser state
