// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammar
open Farkle.IO
open System.Collections.Immutable

/// Functions to tokenize `CharStreams`.
module Tokenizer =

    open Farkle.IO.CharStream

    type private TokenizerState = (CharSpan * Group) list

    let private (|CanEndGroup|_|) x =
        match x with
        | Choice1Of4 term -> Some <| Choice1Of3 term
        | Choice2Of4 noise -> Some <| Choice2Of3 noise
        | Choice3Of4 _ -> None
        | Choice4Of4 groupEnd -> Some <| Choice3Of3 groupEnd

    /// Returns whether to unpin the character(s)
    /// encountered by the tokenizer while being inside a group.
    /// If the group stack's bottom-most container symbol is
    /// a noisy one, then it is unpinned the soonest it is consumed.
    let private shouldUnpinCharactersInsideGroup {ContainerSymbol = g} groupStack =
        let g0 = match g with | Choice1Of2 _terminal -> false | Choice2Of2 _noise -> true
        if List.isEmpty groupStack then
            g0
        else
            match (List.last groupStack) with
            | (_, {ContainerSymbol = Choice1Of2 _terminal}) -> false
            | (_, {ContainerSymbol = Choice2Of2 _noise}) -> true

    let private tokenizeDFA (states: ImmutableArray<DFAState>) (oops: OptimizedOperations) input =
        let rec impl idx currState lastAcceptIdx lastAcceptSym =
            // Apparently, if you bring the function to the
            // innermost scope, it gets optimized away.
            let newToken (sym: DFASymbol) idx: Result<_, char> = (sym, pinSpan input idx) |> Ok
            let mutable nextChar = '\u0103'
            let mutable idxNext = idx
            match readChar input &idxNext &nextChar with
            | false ->
                match lastAcceptSym with
                | Some sym -> newToken sym lastAcceptIdx
                | None -> Error input.FirstCharacter
            | true ->
                let newDFA = oops.GetNextDFAState nextChar currState
                if not newDFA.IsError then
                    match states.[newDFA.Value].AcceptSymbol with
                    // We can go further. The DFA did not accept any new symbol.
                    | None -> impl idxNext newDFA lastAcceptIdx lastAcceptSym
                    // We can go further. The DFA has just accepted a new symbol; we take note of it.
                    | Some _ as acceptSymbol -> impl idxNext newDFA idx acceptSymbol
                else
                    match lastAcceptSym with
                    // We can't go further, but the DFA had accepted a symbol in the past; we finish it up until there.
                    | Some sym -> newToken sym lastAcceptIdx
                    // We can't go further, and the DFA had never accepted a symbol; we mark the first character as unrecognized.
                    | None -> Error input.FirstCharacter
        // We have to first check if more input is available.
        // If not, this is the only place we can report an EOF.
        if input.TryLoadFirstCharacter() then
            impl input.CurrentIndex DFAStateTag.InitialState input.CurrentIndex None |> Some
        else
            None

    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize (groups: ImmutableArray<_>) states oops fTransform fMessage (input: CharStream) =
        let rec impl (gs: TokenizerState) =
            let fail msg: Token option = Message (input.CurrentPosition, msg) |> ParseError |> raise
            let newToken sym (cs: CharSpan) =
                let data = unpinSpanAndGenerate sym fTransform input cs
                let theHolyToken = Token.Create input.LastUnpinnedSpanPosition sym data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let leaveGroup tok g gs =
                // There are three cases when we leave a group
                match gs, g.ContainerSymbol with
                // We have now left the group, but the whole group was noise.
                | [], Choice2Of2 _ -> impl []
                // We have left the group and it was a terminal.
                | [], Choice1Of2 sym -> newToken sym tok
                // There is still another outer group. We append the outgoing group's data to the next top group.
                | (tok2, g2) :: xs, _ -> impl ((concatSpans tok tok2, g2) :: xs)
            let tok = tokenizeDFA states oops input
            match tok, gs with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We advance the input, and return the token.
            | Some (Ok (Choice1Of4 term, tok)), [] ->
                advance false input tok
                newToken term tok
            // We found noise outside of any group.
            // We discard it, unpin its characters, and proceed.
            | Some (Ok (Choice2Of4 _noise, tok)), [] ->
                advance true input tok
                impl []
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, advance the input, and continue.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), tok)), gs
                when gs.IsEmpty || (snd gs.Head).Nesting.Contains tokGroupIdx ->
                    let g = groups.[int tokGroupIdx]
                    advance (shouldUnpinCharactersInsideGroup g gs) input tok
                    impl ((tok, g) :: gs)
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, this end symbol might be kept.
            | Some (Ok (CanEndGroup gSym, tok)), (popped, poppedGroup) :: xs
                when poppedGroup.End = gSym ->
                let popped =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        advance (shouldUnpinCharactersInsideGroup poppedGroup xs) input tok
                        concatSpans popped tok
                    | EndingMode.Open -> popped
                leaveGroup popped poppedGroup xs
            // If input ends outside of a group, it's OK.
            | None, [] ->
                input.CurrentPosition |> ParseMessage.EndOfInput |> fMessage
                None
            // We are still inside a group.
            | Some tokenMaybe, (tok2, g2) :: xs ->
                let data =
                    let doUnpin = shouldUnpinCharactersInsideGroup g2 xs
                    match g2.AdvanceMode, tokenMaybe with
                    // We advance the input by the entire token.
                    | AdvanceMode.Token, Ok (_, data) ->
                        advance doUnpin input data
                        concatSpans tok2 data
                    // Or by just one character.
                    | AdvanceMode.Character, _ | _, Error _ ->
                        advanceByOne doUnpin input
                        extendSpanByOne input tok2
                impl ((data, g2) :: xs)
            // If a group end symbol is encountered but outside of any group,
            | Some (Ok (Choice4Of4 ge, _)), [] -> fail <| ParseErrorType.UnexpectedGroupEnd ge
            // or input ends while we are inside a group, unless the group ends with a newline, were we leave the group,
            | None, (gTok, g) :: gs when g.IsEndedByNewline -> leaveGroup gTok g gs
            // then it's an error.
            | None, (_, g) :: _ -> fail <| ParseErrorType.UnexpectedEndOfInput g
            // But if a group starts inside a group that cannot be nested at, it is an error.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx)), _)), ((_, g) :: _) ->
                fail <| ParseErrorType.CannotNestGroups(groups.[int tokGroupIdx], g)
            /// If a group starts while being outside of any group...
            /// Wait a minute! Haven't we already checked this case?
            /// Ssh, don't tell the compiler. She doesn't know about it. 😊
            | Some (Ok (Choice3Of4 _, _)), [] ->
                failwith "Impossible case: The group stack was already checked to be empty."
            // We found an unrecognized symbol while being outside a group. This is an error.
            | Some (Error x), [] -> fail <| ParseErrorType.LexicalError x
        impl []
