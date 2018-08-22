// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.HybridStream
open Farkle.Monads

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Tokenizer =

    type private TokenizerState =
        {
            InputStream: char HybridStream
            CurrentPosition: Position
            GroupStack: Token list
        }
        with
            static member InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
            static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
            static member GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})
            static member Create input = {InputStream = input; CurrentPosition = Position.initial; GroupStack = []}

    open State

    let private getLookAheadBuffer n = HybridStream.takeSafe n >> String.ofList

    let rec private consumeBuffer n = state {
        let consumeSingle = state {
            let! x = getOptic TokenizerState.InputStream_
            match x with
            | HSCons(x, xs) ->
                do! setOptic TokenizerState.InputStream_ xs
                match x with
                | LF ->
                    let! c = getOptic TokenizerState.CurrentPosition_ <!> Position.column
                    if c > 1u then
                        do! mapOptic TokenizerState.CurrentPosition_ Position.incLine
                | CR -> do! mapOptic TokenizerState.CurrentPosition_ Position.incLine
                | _ -> do! mapOptic TokenizerState.CurrentPosition_ Position.incCol
            | HSNil -> do ()
        }
        match n with
        | 0u -> do ()
        | n ->
            do! consumeSingle
            do! consumeBuffer (n - 1u)
    }

    let private tokenizeDFA {InitialState = initialState; States = states} {CurrentPosition = pos; InputStream = input} =
        let newToken = Token.Create pos
        let lookupEdges edges x = edges |> List.tryFind (fst >> flip RangeSet.contains x) |> Option.map (snd >> SafeArray.retrieve states)
        let rec impl currPos x (currState: DFAState) lastAccept =
            match x with
            | HSNil ->
                match lastAccept with
                | Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym
                | None -> newToken EndOfFile ""
            | HSCons(x, xs) ->
                let newDFA =
                    currState.Edges
                    |> List.tryFind (fst >> flip RangeSet.contains x)
                    |> Option.map (snd >> SafeArray.retrieve states)
                let impl = impl (currPos + 1u) xs
                match currState, newDFA, lastAccept with
                | DFAAccept (_, (acceptSymbol, _)), Some newDFA, _ -> impl newDFA (Some (acceptSymbol, currPos))
                | DFAContinue _, Some newDFA, lastAccept -> impl newDFA lastAccept
                | _, None, Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym
                | _, None, None -> input |> getLookAheadBuffer 1u |> newToken Error
        impl 1u input initialState None

    let private produceToken dfa groups = state {
        let rec impl() = state {
            let! x = get <!> (tokenizeDFA dfa)
            let! groupStackTop = getOptic TokenizerState.GroupStack_ <!> List.tryHead
            let nestGroup =
                match x.Symbol with
                | GroupStart _ | GroupEnd _ ->
                    Maybe.maybe {
                        let! groupStackTop = groupStackTop
                        let! gsTopGroup = groupStackTop.Symbol |> Group.getSymbolGroup groups
                        let! myIndex = x.Symbol |> Group.getSymbolGroupIndexed groups
                        return gsTopGroup.Nesting.Contains myIndex
                    } |> Option.defaultValue true
                | _ -> false
            if nestGroup then
                do! x.Data |> String.length |> consumeBuffer
                let newToken = Optic.set Token.Data_ "" x
                do! mapOptic TokenizerState.GroupStack_ (List.cons newToken)
                return! impl()
            else
                match groupStackTop with
                | None ->
                    do! x.Data |> String.length |> consumeBuffer
                    return x
                | Some groupStackTop ->
                    let groupStackTopGroup =
                        groupStackTop.Symbol
                        |> Group.getSymbolGroup groups
                        |> mustBeSome // I am sorry. 😭
                    if groupStackTopGroup.EndSymbol = x.Symbol then
                        let! pop = state {
                            do! mapOptic TokenizerState.GroupStack_ List.tail
                            if groupStackTopGroup.EndingMode = Closed then
                                do! x.Data |> String.length |> consumeBuffer
                                return groupStackTop |> Token.AppendData x.Data
                            else
                                return groupStackTop
                        }
                        let! groupStackTop = getOptic (TokenizerState.GroupStack_ >-> List.head_)
                        match groupStackTop with
                            | Some _ ->
                                do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                                return! impl()
                            | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                    elif x.Symbol = EndOfFile then
                        return x
                    else
                        match groupStackTopGroup.AdvanceMode with
                        | Token ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData x.Data)
                            do! x.Data |> String.length |> consumeBuffer
                        | Character ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (x.Data.[0] |> string |> Token.AppendData)
                            do! consumeBuffer 1u
                        return! impl()
        }
        let! token = impl()
        let! isGroupStackEmpty = getOptic TokenizerState.GroupStack_ <!> List.isEmpty
        let! currentPosition = getOptic TokenizerState.CurrentPosition_
        return {NewToken = token; IsGroupStackEmpty = isGroupStackEmpty; CurrentPosition = currentPosition}
    }

    let create dfa groups input: Tokenizer = EndlessProcess.ofState (produceToken dfa groups) (TokenizerState.Create input)
