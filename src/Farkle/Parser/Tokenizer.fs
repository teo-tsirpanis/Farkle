// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammar
open Farkle.IO
open System
open System.Collections.Immutable

/// Functions to tokenize `CharStreams`.
module Tokenizer =

    open Farkle.IO.CharStream

    type private TokenizerState = (uint64 * Group) list

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

    // Unfortunately we can't have ref structs on inner functions yet.
    let rec private tokenizeDFA_impl (states: ImmutableArray<DFAState>) (oops: OptimizedOperations) (input: CharStream)
        ofs currState lastAcceptOfs lastAcceptSym (span: ReadOnlySpan<_>) =
        if ofs = span.Length then
            if input.TryExpandPastOffset(ofs) then
                tokenizeDFA_impl states oops input ofs currState lastAcceptOfs lastAcceptSym input.CharacterBuffer
            else
                match lastAcceptSym with
                | Some sym -> Ok sym, (input.ConvertOffsetToIndex lastAcceptOfs)
                | None -> Error '\000', (input.ConvertOffsetToIndex ofs)
        else
            let c = span.[ofs]
            let newDFA = oops.GetNextDFAState c currState
            if newDFA.IsOk then
                match states.[newDFA.Value].AcceptSymbol with
                | None -> tokenizeDFA_impl states oops input (ofs + 1) newDFA lastAcceptOfs lastAcceptSym span
                | Some _ as acceptSymbol -> tokenizeDFA_impl states oops input (ofs + 1) newDFA ofs acceptSymbol span
            else
                match lastAcceptSym with
                | Some sym -> Ok sym, (input.ConvertOffsetToIndex lastAcceptOfs)
                | None -> Error c, (input.ConvertOffsetToIndex ofs)

    let private tokenizeDFA states oops (input: CharStream) =
        if input.TryExpandPastOffset 0 then
            tokenizeDFA_impl states oops input 0 DFAStateTag.InitialState 0 None input.CharacterBuffer |> Some
        else
            None

    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize (groups: ImmutableArray<_>) states oops fTransform fMessage (input: CharStream) =
        let rec impl (gs: TokenizerState) =
            let fail msg: Token option = Message (input.CurrentPosition, msg) |> ParserError |> raise
            let newToken sym idx =
                let data = unpinSpanAndGenerateObject sym fTransform input idx
                let theHolyToken = Token.Create input.LastTokenPosition sym data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let leaveGroup idx g gs =
                // There are three cases when we leave a group
                match gs, g.ContainerSymbol with
                // We have now left the group, but the whole group was noise.
                | [], Choice2Of2 _ -> impl []
                // We have left the group and it was a terminal.
                | [], Choice1Of2 sym -> newToken sym idx
                // There is still another outer group.
                | gs, _ -> impl gs
            let tok = tokenizeDFA states oops input
            match tok, gs with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We advance the input, and return the token.
            | Some (Ok (Choice1Of4 term), idx), [] ->
                input.StartNewToken()
                advance false input idx
                newToken term idx
            // We found noise outside of any group.
            // We discard it, unpin its characters, and proceed.
            | Some (Ok (Choice2Of4 _noise), idx), [] ->
                advance true input idx
                impl []
            // A new group just started, and it was found by its symbol in the group table.
            // If we are already in a group, we check whether it can be nested inside this new one.
            // If it can (or we were not in a group previously), push the token and the group
            // in the group stack, advance the input, and continue.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx))), idx), gs
                when gs.IsEmpty || (snd gs.Head).Nesting.Contains tokGroupIdx ->
                    let g = groups.[int tokGroupIdx]
                    input.StartNewToken()
                    advance (shouldUnpinCharactersInsideGroup g gs) input idx
                    impl ((idx, g) :: gs)
            // We are inside a group, and this new token is going to end it.
            // Depending on the group's definition, this end symbol might be kept.
            | Some (Ok sym, idx), (poppedIdx, poppedGroup) :: xs when poppedGroup.IsEndedBy sym ->
                let poppedIdx =
                    match poppedGroup.EndingMode with
                    | EndingMode.Closed ->
                        advance (shouldUnpinCharactersInsideGroup poppedGroup xs) input idx
                        idx
                    | EndingMode.Open -> poppedIdx
                leaveGroup poppedIdx poppedGroup xs
            // If input ends outside of a group, it's OK.
            | None, [] ->
                input.CurrentPosition |> ParseMessage.EndOfInput |> fMessage
                None
            // We are still inside a group.
            | Some tokenMaybe, (idx2, g2) :: xs ->
                let newIdx =
                    let doUnpin = shouldUnpinCharactersInsideGroup g2 xs
                    match g2.AdvanceMode, tokenMaybe with
                    // We advance the input by the entire token.
                    | AdvanceMode.Token, (Ok (_), data) ->
                        advance doUnpin input data
                        data
                    // Or by just one character.
                    | AdvanceMode.Character, _ | _, (Error _, _) ->
                        advanceByOne doUnpin input
                        idx2 + 1UL
                impl ((newIdx, g2) :: xs)
            // If a group end symbol is encountered but outside of any group,
            | Some (Ok (Choice4Of4 ge), _), [] -> fail <| ParseErrorType.UnexpectedGroupEnd ge
            // or input ends while we are inside a group, unless the group ends with a newline, were we leave the group,
            | None, (gIdx, g) :: gs when g.IsEndedByNewline -> leaveGroup gIdx g gs
            // then it's an error.
            | None, (_, g) :: _ -> fail <| ParseErrorType.UnexpectedEndOfInput g
            // But if a group starts inside a group that cannot be nested at, it is an error.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx))), _), ((_, g) :: _) ->
                fail <| ParseErrorType.CannotNestGroups(groups.[int tokGroupIdx], g)
            /// If a group starts while being outside of any group...
            /// Wait a minute! Haven't we already checked this case?
            /// Ssh, don't tell the compiler. She doesn't know about it. 😊
            | Some (Ok (Choice3Of4 _), _), [] ->
                failwith "Impossible case: The group stack was already checked to be empty."
            // We found an unrecognized symbol while being outside a group. This is an error.
            | Some (Error c, idx), [] ->
                let errorPos = getPositionAtIndex input idx
                Message(errorPos, ParseErrorType.LexicalError c)
                |> ParserError
                |> raise
        impl []
