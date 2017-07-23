// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads

module Parser =

    open State

    let lookAhead n =
        getOptic ParserState.InputStream_
        <!> List.tryItem n

    let getLookAheadBuffer n x =
        let x = x ^. ParserState.InputStream_ |> StateResult.eval (List.take (int n))
        match x with
        | Ok (x, _) -> x |> String.ofList
        | Bad _ -> ""

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

    let lookAheadDFA state x =
        let rec impl state currPos currState lastAccept lastAccPos x =
            let newToken = state ^. ParserState.CurrentPosition_ |> Token.Create
            let newPos = currPos + 1u
            match x with
            | [] -> newToken Symbol.EOF ""
            | x :: xs ->
                let dfaStates = state ^. ParserState.Grammar_ |> Grammar.dfa
                let newDFA =
                    currState
                    |> DFAState.edges
                    |> Set.toSeq
                    |> Seq.tryFind (fun (cs, _) -> RangeSet.contains cs x)
                    |> Option.bind (snd >> Indexed.getfromList dfaStates)
                match newDFA with
                | Some dfa ->
                    match dfa.AcceptSymbol with
                    | Some x -> impl state newPos dfa (Some x) currPos xs
                    | None -> impl state newPos dfa lastAccept lastAccPos xs
                | None ->
                    match lastAccept with
                    | Some x -> state |> getLookAheadBuffer lastAccPos |> newToken x
                    | None -> state |> getLookAheadBuffer 1u |> newToken Symbol.Error 
        3.1415926535897932384626433
