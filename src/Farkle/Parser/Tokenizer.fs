// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
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
            GroupStack: (Token * Group) list
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
            let! tok = get <!> (tokenizeDFA dfa)
            let! gs = getOptic TokenizerState.GroupStack_
            match tok.Symbol, gs with
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | GroupStart (tokGroupIdx, _), _ when gs |> List.tryHead |> Option.map (snd >> Group.nesting >> Set.contains tokGroupIdx) |> Option.defaultValue true ->
                let tokGroup = SafeArray.retrieve groups tokGroupIdx
                do! tok.Data |> String.length |> consumeBuffer
                do! setOptic TokenizerState.GroupStack_ ((tok, tokGroup) :: gs)
                return! impl()
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | _, [] ->
                do! tok.Data |> String.length |> consumeBuffer
                return tok
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, the end symbol might be kept.
            | sym, (popped, poppedGroup) :: xs when poppedGroup.EndSymbol = sym ->
                let! popped = state {
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        do! tok.Data |> String.length |> consumeBuffer
                        return Token.AppendData tok.Data popped
                    | EndingMode.Open -> return popped
                }
                match xs with
                // We have now left the group. We empty the group stack and and fix the symbol of our token.
                | [] ->
                    do! setOptic TokenizerState.GroupStack_ []
                    return {popped with Symbol = poppedGroup.ContainerSymbol}
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs ->
                    do! setOptic TokenizerState.GroupStack_ ((Token.AppendData popped.Data tok2, g2) :: xs)
                    return! impl()
            // If input ends inside the group, we stop here. The upper-level parser will report the error.
            | EndOfFile, _ -> return tok
            // We are still inside a group. 
            | _, (tok2, g2) :: xs ->
                // The input can advance either by just one character, or the entire token.
                let dataToAdvance =
                    match g2.AdvanceMode with
                    | AdvanceMode.Token -> tok.Data
                    | AdvanceMode.Character -> string tok.Data.[0]
                do! dataToAdvance |> String.length |> consumeBuffer
                do! setOptic TokenizerState.GroupStack_ ((Token.AppendData dataToAdvance tok2, g2) :: xs)
                return! impl()
        }
        let! tok = impl()
        let! isInsideGroup = getOptic TokenizerState.GroupStack_ <!> (List.isEmpty >> not)
        return {NewToken = tok; IsInsideGroup = isInsideGroup}
    }

    let inline private shouldEndAfterThat {NewToken = {Symbol = x}} =
        match x with
        | EndOfFile | Unrecognized -> true
        | Nonterminal _ | Terminal _ | Noise _ | GroupStart _ | GroupEnd _ -> false

    let create dfa groups input: Tokenizer = Extra.State.toSeq shouldEndAfterThat (produceToken dfa groups) (TokenizerState.Create input)
