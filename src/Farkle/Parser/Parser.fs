// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Grammar
open Farkle.Monads

module Parser =

    open State

    let lookAhead n =
        getOptic ParserState.InputStream_
        <!> List.tryItem n

    let consumeBuffer n = state {
        let! len = getOptic ParserState.InputStream_ <!> eval (List.length())
        let consumeSingle = state {
            let! x = lookAhead 1
            match x with
            | Some x ->
                do! mapOptic ParserState.InputStream_ List.tail // We know that the list has items here.
                match x with
                | LF ->
                    let! c = getOptic ParserState.CurrentPosition_ <!> Position.column
                    if c > 1u then
                        do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | CR -> do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | _ -> do! mapOptic ParserState.CurrentPosition_ Position.incCol
            | None -> do ()
        }
        match n with
        | n when n > 0 && n <= len ->
            return! repeatM consumeSingle n |> ignore
        | _ -> do ()
    }

    type DFAParserState = {}

    let lookAheadDFA pos grammar x =
        match x with
        | [] -> {Data = TokenData ""; Position = pos; Symbol = grammar |> Grammar.firstSymbolOfType Error |> Option.v}
