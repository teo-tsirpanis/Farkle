// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Collections
open Farkle.Grammar2
open Farkle.Monads
open Farkle.PostProcessor

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Tokenizer =

    open Farkle.Collections.CharStream
    
    type private TokenDraft = {Position: Position; DraftData: CharSpan}

    type private TokenizerState = (TokenDraft * Group) list

    let private tokenizeDFA {InitialState = initialState; States = states} (input: CharStream) =
        let newToken sym v = (sym, {Position = input.Position; DraftData = pinSpan v}) |> Ok |> Some
        let lookupEdges x = RangeMap.tryFind x >> Option.map (SafeArray.retrieve states)
        let rec impl v (currState: DFAState) lastAccept =
            match v with
            | CSNil ->
                match lastAccept with
                | Some (sym, v) -> newToken sym v
                | None -> None
            | CSCons(x, xs) ->
                let newDFA = lookupEdges x currState.Edges
                let impl = impl xs
                match newDFA, lastAccept with
                // We can go further. The DFA did not accept any new symbol.
                | Some (DFAState.Continue _ as newDFA), lastAccept -> impl newDFA lastAccept
                // We can go further. The DFA has just accepted a new symbol; we take note of it.
                | Some (DFAState.Accept (_, acceptSymbol, _) as newDFA), _ -> impl newDFA (Some (acceptSymbol, v))
                // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                | None, Some (sym, v) -> newToken sym v
                // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                | None, None -> input.FirstCharacter |> Error |> Some
        impl (view input) initialState None

    let private produceToken dfa groups (pp: PostProcessor<_>) input =
        let newToken sym {Position = pos; DraftData = data} =
            ((CharStreamCallback pp.Transform), input, data)
            |||> unpinSpanAndGenerate sym
            |> Token.Create pos sym
            |> TokenizerResult.TokenRead
            |> Ok
        let (@<) src {DraftData = dataToAdvance} = {src with DraftData = extendSpans src.DraftData dataToAdvance}
        let rec impl (gs: TokenizerState) =
            let tok = tokenizeDFA dfa input
            match tok, gs with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | Some (Ok (Choice1Of4 term, tok)), [] ->
                consume input tok.DraftData
                newToken term tok
            // We found noise outside of any group.
            // We consume it, and proceed.
            | Some (Ok (Choice2Of4 _noise, tok)), [] ->
                consume input tok.DraftData
                impl []
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), tok)), gs
                when gs.IsEmpty || (snd gs.Head).Nesting.Contains tokGroupIdx ->
                let tokGroup = SafeArray.retrieve groups tokGroupIdx
                consume input tok.DraftData
                impl ((tok, tokGroup) :: gs)
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, the end symbol might be kept.
            | Some (Ok (Choice4Of4 gSym, tok)), (popped, poppedGroup) :: xs
                when poppedGroup.End = gSym ->
                let popped =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        consume input tok.DraftData
                        popped @< tok
                    | EndingMode.Open -> popped
                match xs, poppedGroup.ContainerSymbol with
                // We have now left the group, but the whole group was noise.
                | [], Choice2Of2 _ -> impl []
                // We have left the group and it was a terminal.
                | [], Choice1Of2 sym -> newToken sym popped
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs, _ -> impl ((popped @< tok2, g2) :: xs)
            // If input ends outside of a group, it's OK.
            | None, [] -> Ok <| TokenizerResult.EndOfInput input.Position
            // If a group starts inside a group that cannot be nested at,
            | Some (Ok (Choice3Of4 _, _)), _
            // or a group end symbol is encountered but does not actually end the group,
            | Some (Ok (Choice4Of4 _, _)), _
            // or input ends while we are inside a group,
            | None, _ :: _ -> Error <| TokenizerResult.GroupError input.Position // then it's an error.
            // We found an unrecognized symbol while outside a group. This is an error.
            | Some (Error x), [] -> Error <| TokenizerResult.LexicalError (x, input.Position)
            // We are still inside a group. 
            | Some tokenMaybe, (tok2, g2) :: xs ->
                let data =
                    match g2.AdvanceMode, tokenMaybe with
                    // We advance the input by the entire token.
                    | AdvanceMode.Token, Ok (_, {DraftData = data}) ->
                        consume input data
                        extendSpans tok2.DraftData data
                    // Or by just one character.
                    | AdvanceMode.Character, _ | _, Error _ ->
                        consumeOne input
                        extendSpanByOne input tok2.DraftData
                impl (({tok2 with DraftData = data}, g2) :: xs)
        impl

    let create dfa groups pp input: Tokenizer = Extra.State.toSeq (produceToken dfa groups pp input) []
