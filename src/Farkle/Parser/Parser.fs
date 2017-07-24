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

    let getLookAheadBuffer n x =
        let n = System.Math.Min(int n, Collections.List.length x)
        let x = x |> StateResult.eval (List.take n)
        match x with
        | Ok (x, _) -> x |> String.ofList
        | Bad _ -> ""

    let consumeBuffer n = state {
        let! len = getOptic ParserState.InputStream_ <!> eval (List.length())
        let consumeSingle = state {
            let! x = getOptic (ParserState.InputStream_ >-> List.head_)
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

    // Pascal code (ported from Java 💩): 72 lines of begin/ends, mutable hell and unreasonable garbage.
    // F# code: 22 lines of clear, reasonable and type-safe code. I am so confident and would not even test it!
    // This is a 30.5% decrease of code and a 30.5% increase of productivity. Why do __You__ still code in C# (☹)? Or Java (😠)?
    let tokenizeDFA dfaStates initialState pos input =
        let rec impl currPos currState lastAccept lastAccPos x =
            let newToken = Token.Create pos
            let newPos = currPos + 1u
            match x with
            | [] -> newToken Symbol.EOF ""
            | x :: xs ->
                let newDFA =
                    currState
                    |> DFAState.edges
                    |> Set.toSeq
                    |> Seq.tryFind (fun (cs, _) -> RangeSet.contains cs x)
                    |> Option.bind (snd >> Indexed.getfromList dfaStates)
                match newDFA with
                | Some dfa ->
                    match dfa.AcceptSymbol with
                    | Some x -> impl newPos dfa (Some x) currPos xs
                    | None -> impl newPos dfa lastAccept lastAccPos xs
                | None ->
                    match lastAccept with
                    | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x
                    | None -> input |> getLookAheadBuffer 1u |> newToken Symbol.Error
        impl 1u initialState None 0u input
