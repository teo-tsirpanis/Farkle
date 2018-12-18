// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.PostProcessor

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Functions to tokenize `CharStreams`.
module Tokenizer =

    open Farkle.Collections.CharStream

    type private TokenizerState = (CharSpan * Group) list

    let private (|CanEndGroup|_|) x =
        match x with
        | Choice1Of4 term -> Some <| Choice2Of2 term
        | Choice2Of4 _
        | Choice3Of4 _ -> None
        | Choice4Of4 groupEnd -> Some <| Choice1Of2 groupEnd

    let private tokenizeDFA {InitialState = initialState; States = states} (input: CharStream) =
        let newToken sym v = (sym, pinSpan v) |> Ok |> Some
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

    /// Breaks a `CharStream` into a series of post-processed tokens, according to the given `Grammar`.
    /// This function is pretty complicated, so let's enumerate the parameters' purpose.
    /// The function accepts:
    /// 1. a function that determines the result of _this_ function, in case an error occurs.
    /// 2. a function that accepts:
    ///     * the current position (it is redundant if a token is specified, but not when input ends)
    ///     * a token that was just generated (or `None` in case of an end of input).
    ///     * the function's state.
    ///     and returns
    ///     * the result of _this_ function, or `None`, if it should be given another token.
    ///     * the function's new state.
    /// 3. the initial state of the previous function.
    /// 4. the `Grammar` to use.
    /// 5. the `PostProcessor` to use on the newly-transformd tokens.
    /// 6. the `CharStream` to act as an input. __Remember that `CharStream`s are _not_ thread-safe.__
    let tokenize fError fToken fTokenS0 {_DFAStates = dfa; _Groups = groups} (pp: PostProcessor<_>) (input: CharStream) =
        let fError msg = Message (input.Position, msg) |> fError
        // let (@<) src dataToAdvance = {src with DraftData = extendSpans src.DraftData dataToAdvance}
        let rec impl (gs: TokenizerState) fTokenS =
            let fToken pos t =
                match fToken pos t fTokenS with
                | Some x, _ -> x
                | None, s -> impl [] s
            let newToken sym data =
                let (data, pos) = unpinSpanAndGenerate sym (CharStreamCallback (fun sym pos data -> pp.Transform(sym, pos, data))) input data
                Token.Create pos sym data |> Some |> fToken pos
            let tok = tokenizeDFA dfa input
            match tok, gs with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | Some (Ok (Choice1Of4 term, tok)), [] ->
                consume input tok
                newToken term tok
            // We found noise outside of any group.
            // We consume it, and proceed.
            | Some (Ok (Choice2Of4 _noise, tok)), [] ->
                consume input tok
                impl [] fTokenS
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), tok)), gs ->
                let tokGroup = SafeArray.retrieve groups tokGroupIdx
                if gs.IsEmpty || (snd gs.Head).Nesting.Contains tokGroupIdx then
                    consume input tok
                    impl ((tok, tokGroup) :: gs) fTokenS
                // But if a group starts inside a group that cannot be nested at,
                else
                    // it is an error.
                    // I can call gs.Head, because the group stack cannot be empty here.
                    fError <| ParseErrorType.CannotNestGroups(tokGroup, snd gs.Head)
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, this end symbol might be kept.
            | Some (Ok (CanEndGroup gSym, tok)), (popped, poppedGroup) :: xs
                when poppedGroup.End = gSym ->
                let popped =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        consume input tok
                        extendSpans popped tok
                    | EndingMode.Open -> popped
                match xs, poppedGroup.ContainerSymbol with
                // We have now left the group, but the whole group was noise.
                | [], Choice2Of2 _ -> impl [] fTokenS
                // We have left the group and it was a terminal.
                | [], Choice1Of2 sym -> newToken sym popped
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs, _ -> impl ((extendSpans popped tok2, g2) :: xs) fTokenS
            // If input ends outside of a group, it's OK.
            | None, [] -> fToken input.Position None
            // If a group end symbol is encountered but outside of any group,
            | Some (Ok (Choice4Of4 ge, _)), [] -> fError <| ParseErrorType.UnexpectedGroupEnd ge
            // or input ends while we are inside a group,
            | None, (_, g) :: _ -> fError <| ParseErrorType.UnexpectedEndOfInput g // then it's an error.
            // We found an unrecognized symbol while being outside a group. This is an error.
            | Some (Error x), [] -> fError <| ParseErrorType.LexicalError x
            // We are still inside a group.
            | Some tokenMaybe, (tok2, g2) :: xs ->
                let data =
                    match g2.AdvanceMode, tokenMaybe with
                    // We advance the input by the entire token.
                    | AdvanceMode.Token, Ok (_, data) ->
                        consume input data
                        extendSpans tok2 data
                    // Or by just one character.
                    | AdvanceMode.Character, _ | _, Error _ ->
                        consumeOne input
                        extendSpanByOne input tok2
                impl ((data, g2) :: xs) fTokenS
        impl [] fTokenS0
