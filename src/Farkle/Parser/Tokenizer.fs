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

    // Unfortunately we can't have ref structs on inner functions yet.
    let rec private tokenizeDFA_impl (states: ImmutableArray<DFAState>) (oops: OptimizedOperations) (input: CharStream)
        ofs currState lastAcceptOfs lastAcceptSym (span: ReadOnlySpan<_>) =
        if ofs = span.Length then
            if input.TryExpandPastOffset(ofs) then
                tokenizeDFA_impl states oops input ofs currState lastAcceptOfs lastAcceptSym input.CharacterBuffer
            else
                match lastAcceptSym with
                | Some sym -> Ok sym, lastAcceptOfs
                | None -> Error(), ofs
        else
            let c = span.[ofs]
            let newDFA = oops.GetNextDFAState c currState
            if newDFA.IsOk then
                match states.[newDFA.Value].AcceptSymbol with
                | None -> tokenizeDFA_impl states oops input (ofs + 1) newDFA lastAcceptOfs lastAcceptSym span
                | Some _ as acceptSymbol -> tokenizeDFA_impl states oops input (ofs + 1) newDFA ofs acceptSymbol span
            else
                match lastAcceptSym with
                | Some sym -> Ok sym, lastAcceptOfs
                | None -> Error(), ofs

    let private tokenizeDFA states oops (input: CharStream) =
        if input.TryExpandPastOffset 0 then
            tokenizeDFA_impl states oops input 0 DFAStateTag.InitialState 0 None input.CharacterBuffer |> Some
        else
            None

    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize (groups: ImmutableArray<_>) states oops fTransform fMessage (input: CharStream) =
        let fail msg = Message (input.CurrentPosition, msg) |> ParserError |> raise
        let rec groupLoop isNoiseGroup groupStack =
            match groupStack with
            | [] -> ()
            | currentGroup :: gs ->
                let tok = tokenizeDFA states oops input
                match tok with
                // A new group begins that is allowed to nest into this one.
                | Some(Ok(Choice3Of4(GroupStart(_, tokGroupIdx))), idx)
                    when currentGroup.Nesting.Contains tokGroupIdx ->
                        let g = groups.[int tokGroupIdx]
                        advance isNoiseGroup input idx
                        groupLoop isNoiseGroup (g :: groupStack)
                // A symbol is found that ends the current group.
                | Some(Ok sym, ofs) when currentGroup.IsEndedBy sym ->
                    match currentGroup.EndingMode with
                    | EndingMode.Closed ->
                        advance isNoiseGroup input ofs
                    | EndingMode.Open -> ()
                    groupLoop isNoiseGroup gs
                // The existing group is continuing.
                | Some tokenMaybe ->
                    match currentGroup.AdvanceMode, tokenMaybe with
                    | AdvanceMode.Token, (Ok _, ofs) ->
                        advance isNoiseGroup input ofs
                    | AdvanceMode.Character, _ | _, (Error _, _) ->
                        advanceByOne isNoiseGroup input
                    groupLoop isNoiseGroup groupStack
                // Input ended and the current group can be ended by a newline.
                | None when currentGroup.IsEndedByNewline -> groupLoop isNoiseGroup gs
                // Input ended unexpectedly.
                | None -> fail <| ParseErrorType.UnexpectedEndOfInputInGroup currentGroup
        let rec tokenLoop() =
            let newToken term =
                let data = unpinSpanAndGenerateObject term fTransform input
                let theHolyToken = Token.Create input.LastTokenPosition term data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let tok = tokenizeDFA states oops input
            match tok with
            // We are neither inside any group, nor a new one is going to start.
            // The easiest case. We advance the input, and return the token.
            | Some (Ok (Choice1Of4 term), ofs) ->
                input.StartNewToken()
                advance false input ofs
                newToken term
            // We found noise outside of any group.
            // We discard it, unpin its characters, and proceed.
            | Some (Ok (Choice2Of4 _noise), ofs) ->
                advance true input ofs
                tokenLoop()
            // A new group just started. We will enter the group loop function.
            | Some (Ok (Choice3Of4 (GroupStart (_, tokGroupIdx))), ofs) ->
                let g = groups.[int tokGroupIdx]
                input.StartNewToken()
                let isNoise = not g.IsTerminal
                advance isNoise input ofs
                groupLoop isNoise [g]
                match g.ContainerSymbol with
                // The group is a terminal. We return it.
                | Choice1Of2 term -> newToken term
                // The group had been noise all along. We discard it and move on.
                | Choice2Of2 _ -> tokenLoop()
            // Input ends outside of a group.
            | None ->
                input.CurrentPosition |> ParseMessage.EndOfInput |> fMessage
                None
            // A group end symbol is encountered but outside of any group.
            | Some (Ok (Choice4Of4 ge), _) -> fail <| ParseErrorType.UnexpectedGroupEnd ge
            // We found an unrecognized symbol while being outside a group. This is an error.
            | Some (Error(), ofs) ->
                let errorPos = getPositionAtOffset input ofs
                let errorType =
                    let span = input.CharacterBuffer
                    if ofs = span.Length then
                        ParseErrorType.UnexpectedEndOfInput
                    else
                        ParseErrorType.LexicalError span.[ofs]
                Message(errorPos, errorType)
                |> ParserError
                |> raise
        tokenLoop()
