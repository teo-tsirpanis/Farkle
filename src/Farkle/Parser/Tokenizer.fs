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
        let lookupEdges x = List.tryFind (fst >> SetEx.contains x) >> Option.map (snd >> SafeArray.retrieve states)
        let rec impl currPos x (currState: DFAState) lastAccept =
            match x with
            | HSNil ->
                match lastAccept with
                | Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym |> Ok
                | None -> newToken EndOfFile "" |> Ok
            | HSCons(x, xs) ->
                let newDFA = lookupEdges x currState.Edges
                let impl = impl (currPos + 1u) xs
                match newDFA, lastAccept with
                // We can go further. The DFA did not accept any new symbol.
                | Some (DFAContinue _ as newDFA), lastAccept -> impl newDFA lastAccept
                // We can go further. The DFA has just accepted a new symbol; we take note of it.
                | Some (DFAAccept (_, (acceptSymbol, _)) as newDFA), _ -> impl newDFA (Some (acceptSymbol, currPos))
                // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                | None, Some (sym, pos) -> input |> getLookAheadBuffer pos |> newToken sym |> Ok
                // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                | None, None -> input |> getLookAheadBuffer 1u |> (fun x -> Error x.[0])
        impl 1u input initialState None

    let private produceToken dfa groups =
        let rec impl (state: TokenizerState) =
            let tok = tokenizeDFA dfa state
            let gs = state.GroupStack
            match tok, gs with
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | Ok({Symbol = GroupStart (tokGroupIdx, _)} as tok), _ when gs |> List.tryHead |> Option.map (fun (_, g) -> g.Nesting.Contains(tokGroupIdx)) |> Option.defaultValue true ->
                let tokGroup = SafeArray.retrieve groups tokGroupIdx
                let state = tok.Data |> String.length |> consumeBuffer <| state |> snd
                impl {state with GroupStack = (tok, tokGroup) :: gs}
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | Ok tok, [] ->
                TokenizerResult.TokenRead tok, tok.Data |> String.length |> consumeBuffer <| state |> snd
            // We found an unrecognized symbol while outside a group. This is an error.
            | Error x, [] -> TokenizerResult.LexicalError (x, state.CurrentPosition), state
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, the end symbol might be kept.
            | Ok tok, (popped, poppedGroup) :: xs when poppedGroup.EndSymbol = tok.Symbol ->
                let popped, state =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        Token.AppendData tok.Data popped, tok.Data |> String.length |> consumeBuffer <| state |> snd
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
                let state =  dataToAdvance |> String.length |> consumeBuffer <| state |> snd
                impl {state with GroupStack = (Token.AppendData dataToAdvance tok2, g2) :: xs}
        impl

    let inline private shouldEndAfterThat x =
        match x with
        | TokenizerResult.TokenRead _ -> false
        | _ -> true

    let create dfa groups input: Tokenizer = Extra.State.toSeq shouldEndAfterThat (produceToken dfa groups) (TokenizerState.Create input)
