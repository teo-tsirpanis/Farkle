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

    let rec private consumeBuffer n (state: TokenizerState) =
        let rec impl n inputStream pos =
            let impl = impl (n - 1u)
            match n, inputStream with
            | 0u, _ | _, HSNil -> inputStream, pos
            | _, HSCons(LF, xs) when Position.column pos > 1u -> impl xs (Position.incLine pos)
            | _, HSCons(CR, xs) -> impl xs (Position.incLine pos)
            | _, HSCons(_, xs) -> impl xs (Position.incCol pos)
        let (inputStream, pos) = impl n state.InputStream state.CurrentPosition
        (), {state with InputStream = inputStream; CurrentPosition = pos}

    let private tokenizeDFA {InitialState = initialState; States = states} {CurrentPosition = pos; InputStream = input} =
        let newToken = Token.Create pos
        let lookupEdges x = List.tryFind (fst >> flip RangeSet.contains x) >> Option.map (snd >> SafeArray.retrieve states)
        let rec impl currPos x (currState: DFAState) lastAccept =
            match x with
            | HSNil ->
                match lastAccept with
                | Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym
                | None -> newToken EndOfFile ""
            | HSCons(x, xs) ->
                let newDFA = lookupEdges x currState.Edges
                let impl = impl (currPos + 1u) xs
                match newDFA, lastAccept with
                // We can go further. The DFA did not accept any new symbol.
                | Some (DFAContinue _ as newDFA), lastAccept -> impl newDFA lastAccept
                // We can go further. The DFA has just accepted a new symbol; we take note of it.
                | Some (DFAAccept (_, (acceptSymbol, _)) as newDFA), _ -> impl newDFA (Some (acceptSymbol, currPos))
                // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                | None, Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym
                // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                | None, None -> input |> getLookAheadBuffer 1u |> newToken Unrecognized
        impl 1u input initialState None

    let private produceToken dfa groups = state {
        let rec impl() = state {
            let! newToken = get <!> (tokenizeDFA dfa)
            let! groupStackTop = getOptic TokenizerState.GroupStack_ <!> List.tryHead
            let nestGroup =
                match newToken.Symbol with
                | GroupStart _ | GroupEnd _ ->
                    Maybe.maybe {
                        let! groupStackTop = groupStackTop
                        let! gsTopGroup = groupStackTop.Symbol |> Group.getSymbolGroup groups
                        let! myIndex = newToken.Symbol |> Group.getSymbolGroupIndexed groups
                        return gsTopGroup.Nesting.Contains myIndex
                    } |> Option.defaultValue true
                | _ -> false
            if nestGroup then
                do! newToken.Data |> String.length |> consumeBuffer
                do! mapOptic TokenizerState.GroupStack_ (List.cons {newToken with Data = ""})
                return! impl()
            else
                match groupStackTop with
                | None ->
                    do! newToken.Data |> String.length |> consumeBuffer
                    return newToken
                | Some groupStackTop ->
                    let groupStackTopGroup =
                        groupStackTop.Symbol
                        |> Group.getSymbolGroup groups
                        |> mustBeSome // I am sorry. 😭
                    if groupStackTopGroup.EndSymbol = newToken.Symbol then
                        let! pop = state {
                            do! mapOptic TokenizerState.GroupStack_ List.tail
                            if groupStackTopGroup.EndingMode = Closed then
                                do! newToken.Data |> String.length |> consumeBuffer
                                return groupStackTop |> Token.AppendData newToken.Data
                            else
                                return groupStackTop
                        }
                        let! groupStackTop = getOptic (TokenizerState.GroupStack_ >-> List.head_)
                        match groupStackTop with
                            | Some _ ->
                                do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                                return! impl()
                            | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                    elif newToken.Symbol = EndOfFile then
                        return newToken
                    else
                        match groupStackTopGroup.AdvanceMode with
                        | Token ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData newToken.Data)
                            do! newToken.Data |> String.length |> consumeBuffer
                        | Character ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (newToken.Data.[0] |> string |> Token.AppendData)
                            do! consumeBuffer 1u
                        return! impl()
        }
        let! token = impl()
        let! isGroupStackEmpty = getOptic TokenizerState.GroupStack_ <!> List.isEmpty
        let! currentPosition = getOptic TokenizerState.CurrentPosition_
        return {NewToken = token; IsGroupStackEmpty = isGroupStackEmpty; CurrentPosition = currentPosition}
    }

    let inline private shouldEndAfterThat {NewToken = {Symbol = x}} =
        match x with
        | EndOfFile | Unrecognized -> true
        | Nonterminal _ | Terminal _ | Noise _ | GroupStart _ | GroupEnd _ -> false

    let create dfa groups input: Tokenizer = Extra.State.toSeq shouldEndAfterThat (produceToken dfa groups) (TokenizerState.Create input)
