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
        let! len = getOptic ParserState.InputStream_ <!> Collections.List.length
        let consumeSingle = state {
            let! x = getOptic ParserState.InputStream_
            match x with
            | x :: xs ->
                do! setOptic ParserState.InputStream_ xs
                match x with
                | LF ->
                    let! c = getOptic ParserState.CurrentPosition_ <!> Position.column
                    if c > 1u then
                        do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | CR -> do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | _ -> do! mapOptic ParserState.CurrentPosition_ Position.incCol
            | [] -> do ()
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
                    |> Option.bind (snd >> Indexed.getfromList dfaStates >> Trial.makeOption)
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

    let inline tokenizeDFAForDummies state =
        let grammar = state |> ParserState.grammar
        tokenizeDFA grammar.DFAStates grammar.InitialStates.DFA state.CurrentPosition state.InputStream

    let rec produceToken() = state {
        let! x = get <!> tokenizeDFAForDummies
        let! grammar = get <!> ParserState.grammar
        let! groupStackTop = getOptic (ParserState.GroupStack_ >-> List.head_)
        let nestGroup =
            match x ^. Token.Symbol_ |> Symbol.symbolType with
            | GroupStart | GroupEnd ->
                Maybe.maybe {
                    let! groupStackTop = groupStackTop
                    let! gsTopGroup = groupStackTop ^. Token.Symbol_ |> Group.getSymbolGroup grammar.Groups
                    let! myIndex = x ^. Token.Symbol_ |> Group.getSymbolGroupIndexed grammar.Groups
                    return gsTopGroup.Nesting.Contains myIndex
                } |> Option.defaultValue true
            | _ -> false
        if nestGroup then
            do! x ^. Token.Data_ |> String.length |> consumeBuffer
            let newToken = Optic.set Token.Data_ "" x
            do! mapOptic ParserState.GroupStack_ (cons newToken)
            return! produceToken()
        else
            match groupStackTop with
            | None ->
                do! x ^. Token.Data_ |> String.length |> consumeBuffer
                return x
            | Some groupStackTop ->
                let groupStackTopGroup =
                    groupStackTop ^. Token.Symbol_
                    |> Group.getSymbolGroup grammar.Groups
                    |> mustBeSome // I am sorry. 😭
                if groupStackTopGroup.EndSymbol = x.Symbol then
                    let! pop = state {
                        do! mapOptic ParserState.GroupStack_ List.tail
                        if groupStackTopGroup.EndingMode = Closed then
                            do! x ^. Token.Data_ |> String.length |> consumeBuffer
                            return groupStackTop |> Token.AppendData x.Data
                        else
                            return groupStackTop
                    }
                    let! groupStackTop = getOptic (ParserState.GroupStack_ >-> List.head_)
                    match groupStackTop with
                        | Some _ ->
                            do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                            return! produceToken()
                        | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                elif x.Symbol.SymbolType = EndOfFile then
                    return x
                else
                    match groupStackTopGroup.AdvanceMode with
                    | Token ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData x.Data)
                        do! x ^. Token.Data_ |> String.length |> consumeBuffer
                    | Character ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (x.Data |> Seq.head |> sprintf "%c" |> Token.AppendData)
                        do! consumeBuffer 1
                    return! produceToken()
    }
