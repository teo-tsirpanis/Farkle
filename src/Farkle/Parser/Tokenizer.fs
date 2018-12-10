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

    open Farkle.Collections.CharStream
    
    type TokenDraft = {Symbol: Symbol; Position: Position; Data: CharSpan}

    type private TokenizerState = (TokenDraft * Group) list

    let private tokenizeDFA {InitialState = initialState; States = states} (input: CharStream) =
        let newToken sym data = Choice1Of2 {Symbol = sym; Position = input.Position; Data = pinSpan data}
        let eof = Choice2Of2 input.Position
        let lookupEdges x = RangeMap.tryFind x >> Option.map (SafeArray.retrieve states)
        let rec impl v (currState: DFAState) lastAccept =
            match v with
            | CSNil ->
                match lastAccept with
                | Some (sym, v) -> newToken sym v |> Ok
                | None -> input.Position |> Choice2Of2 |> Ok
            | CSCons(x, xs) ->
                let newDFA = lookupEdges x currState.Edges
                let impl = impl xs
                match newDFA, lastAccept with
                // We can go further. The DFA did not accept any new symbol.
                | Some (DFAContinue _ as newDFA), lastAccept -> impl newDFA lastAccept
                // We can go further. The DFA has just accepted a new symbol; we take note of it.
                | Some (DFAAccept (_, (acceptSymbol, _)) as newDFA), _ -> impl newDFA (Some (acceptSymbol, v))
                // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                | None, Some (sym, v) -> newToken sym v |> Ok
                // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                | None, None -> Error input.FirstCharacter
        impl (view input) initialState None

    let private produceToken dfa groups input =
        let rec impl gs =
            let tok = tokenizeDFA dfa input
            match tok, gs with
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | Ok({Symbol = GroupStart (tokGroupIdx, _)} as tok), _ when
                gs |> List.tryHead |> Option.map (fun (_, g) -> g.Nesting.Contains(tokGroupIdx)) |> Option.defaultValue true ->
                let tokGroup = SafeArray.retrieve groups tokGroupIdx
                let state = tok.Data |> String.length |> consumeBuffer <| state
                impl {state with GroupStack = (tok, tokGroup) :: gs}
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | Ok tok, [] ->
                TokenizerResult.TokenRead tok, tok.Data |> String.length |> consumeBuffer <| state
            // We found an unrecognized symbol while outside a group. This is an error.
            | Error x, [] -> TokenizerResult.LexicalError (x, state.CurrentPosition), state
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, the end symbol might be kept.
            | Ok tok, (popped, poppedGroup) :: xs when poppedGroup.EndSymbol = tok.Symbol ->
                let popped, state =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        Token.AppendData tok.Data popped, tok.Data |> String.length |> consumeBuffer <| state
                    | EndingMode.Open -> popped, state
                match xs with
                // We have now left the group. We empty the group stack and and fix the symbol of our token.
                | [] ->
                    TokenizerResult.TokenRead {popped with Symbol = poppedGroup.ContainerSymbol}, {state with GroupStack = []}
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs -> impl {state with GroupStack = (Token.AppendData popped.Data tok2, g2) :: xs}
            // If input ends inside the group, this is an error.
            | Ok {Symbol = EndOfFile}, _ :: __ -> TokenizerResult.GroupError state.CurrentPosition, state
            // We are still inside a group. 
            | res, (tok2, g2) :: xs ->
                let data = res |> tee (fun x -> x.Data) string
                // The input can advance either by just one character, or the entire token.
                let dataToAdvance =
                    match g2.AdvanceMode with
                    | AdvanceMode.Token -> data
                    | AdvanceMode.Character -> string data.[0]
                let state =  dataToAdvance |> String.length |> consumeBuffer <| state
                impl {state with GroupStack = (Token.AppendData dataToAdvance tok2, g2) :: xs}
        impl

    let create dfa groups input: Tokenizer = Extra.State.toSeq (produceToken dfa groups input) []
