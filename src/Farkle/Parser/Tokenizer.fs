// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar

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

    /// Returns whether to unpin the character(s) encountered by the tokenizer while being inside a group.
    /// If the group stack's bottom-most container symbol is a noisy one, then it is unpinned the soonest it is consumed.
    let private shouldUnpinCharactersInsideGroup {ContainerSymbol = g} groupStack =
        let g0 = match g with | Choice1Of2 _terminal -> false | Choice2Of2 _noise -> true
        let rec impl =
            function
            | [] -> g0
            | [(_, {ContainerSymbol = Choice1Of2 _terminal})] -> false
            | [(_, {ContainerSymbol = Choice2Of2 _noise})] -> true
            | (_, _) :: gs -> impl gs
        impl groupStack

    let private tokenizeDFA {InitialState = initialState; States = states} (input: CharStream) =
        let inline newToken sym idx = struct (sym, pinSpan input idx) |> Ok |> Some
        let retrieve state = SafeArray.retrieve states state
        let rec impl idx (currState: DFAState) lastAccept =
            let mutable x = '\u0103'
            let mutable idxNext = idx
            match readChar input &x &idxNext with
            | false ->
                match lastAccept with
                | ValueSome struct (sym, idx) -> newToken sym idx
                | ValueNone -> None
            | true ->
                let newDFA =
                    RangeMap.tryFind x currState.Edges
                    |> ValueOption.map retrieve
                match newDFA, lastAccept with
                // We can go further. The DFA did not accept any new symbol.
                | ValueSome (DFAState.Continue _ as newDFA), lastAccept -> impl idxNext newDFA lastAccept
                // We can go further. The DFA has just accepted a new symbol; we take note of it.
                | ValueSome (DFAState.Accept (_, acceptSymbol, _) as newDFA), _ -> impl idxNext newDFA (ValueSome struct (acceptSymbol, idx))
                // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                | ValueNone, ValueSome (sym, idx) -> newToken sym idx
                // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                | ValueNone, ValueNone -> input.FirstCharacter |> Error |> Some
        impl (getCurrentIndex input) initialState ValueNone


    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize {_DFAStates = dfa; _Groups = groups} fTransform fMessage (input: CharStream) =
        let fail msg = Message (input.Position, msg) |> ParseError |> raise
        let rec impl (gs: TokenizerState) =
            let newToken sym (cs: CharSpan) =
                let pos = cs.StartingPosition
                let data =
                    try
                        unpinSpanAndGenerate sym fTransform input cs
                    with
                    | ex -> Message(pos, ParseErrorType.TransformError(sym, ex)) |> ParseError |> raise
                let theHolyToken = Token.Create pos sym data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let tok = tokenizeDFA dfa input
            match tok, gs with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We consume the token, and return it.
            | Some (Ok (Choice1Of4 term, tok)), [] ->
                consume false input tok
                newToken term tok
            // We found noise outside of any group.
            // We consume it, unpin its characters, and proceed.
            | Some (Ok (Choice2Of4 _noise, tok)), [] ->
                consume true input tok
                impl []
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, consume the token, and continue.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), tok)), gs
                when gs.IsEmpty || (snd gs.Head).Nesting.Contains tokGroupIdx ->
                    let g = groups.[tokGroupIdx]
                    consume (shouldUnpinCharactersInsideGroup g gs) input tok
                    impl ((tok, g) :: gs)
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, this end symbol might be kept.
            | Some (Ok (CanEndGroup gSym, tok)), (popped, poppedGroup) :: xs
                when poppedGroup.End = gSym ->
                let popped =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        consume (shouldUnpinCharactersInsideGroup poppedGroup xs) input tok
                        extendSpans popped tok
                    | EndingMode.Open -> popped
                match xs, poppedGroup.ContainerSymbol with
                // We have now left the group, but the whole group was noise.
                | [], Choice2Of2 _ -> impl []
                // We have left the group and it was a terminal.
                | [], Choice1Of2 sym -> newToken sym popped
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs, _ -> impl ((extendSpans popped tok2, g2) :: xs)
            // If input ends outside of a group, it's OK.
            | None, [] ->
                input.Position |> ParseMessage.EndOfInput |> fMessage
                None
            // We are still inside a group.
            | Some tokenMaybe, (tok2, g2) :: xs ->
                let data =
                    let doUnpin = shouldUnpinCharactersInsideGroup g2 xs
                    match g2.AdvanceMode, tokenMaybe with
                    // We advance the input by the entire token.
                    | AdvanceMode.Token, Ok (_, data) ->
                        consume doUnpin input data
                        extendSpans tok2 data
                    // Or by just one character.
                    | AdvanceMode.Character, _ | _, Error _ ->
                        consumeOne doUnpin input
                        extendSpanByOne input tok2
                impl ((data, g2) :: xs)
            // If a group end symbol is encountered but outside of any group,
            | Some (Ok (Choice4Of4 ge, _)), [] -> fail <| ParseErrorType.UnexpectedGroupEnd ge
            // or input ends while we are inside a group,
            | None, (_, g) :: _ -> fail <| ParseErrorType.UnexpectedEndOfInput g // then it's an error.
            // But if a group starts inside a group that cannot be nested at, it is an error.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), _)), ((_, g) :: _) ->
                fail <| ParseErrorType.CannotNestGroups(groups.[tokGroupIdx], g)
            /// If a group starts while being outside of any group...
            /// Wait a minute! Haven't we already checked this case?
            /// Ssh, don't tell the compiler. She doesn't know about it. 😊
            | Some (Ok (Choice3Of4 _, _)), [] -> failwith "Impossible case: The group stack was already checked to be empty."
            // We found an unrecognized symbol while being outside a group. This is an error.
            | Some (Error x), [] -> fail <| ParseErrorType.LexicalError x
        impl []
