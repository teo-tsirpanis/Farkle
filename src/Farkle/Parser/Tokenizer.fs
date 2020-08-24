// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammar
open Farkle.IO
open System
open System.Collections.Immutable

[<Struct>]
/// A value type representing the result of a DFA tokenizer invocation.
type private DFAResult private(symbol: DFASymbol, offset: int) =
    static let eof = DFAResult(Unchecked.defaultof<_>, -1)
    /// Creates a `DFAResult` from the last accepted symbol
    /// (if exists), the offset of the last accepted character,
    /// and the offset of the last character the tokenizer is currently.
    static member Create symbolMaybe lastAcceptOfs ofs =
        match symbolMaybe with
        | Some sym -> DFAResult(sym, lastAcceptOfs)
        | None -> DFAResult(Unchecked.defaultof<_>, ofs)
    /// A `DFAResult` signifying that input ended.
    static member EOF = eof
    /// Whether this `DFAResult` signifies that a new token was found.
    member _.FoundToken = not(obj.ReferenceEquals(symbol, Unchecked.defaultof<_>))
    /// The symbol of the token that was found. Before using
    /// this property, check that `FoundToken` is true.
    member _.Symbol = symbol
    /// Whether this `DFAResult` signifies that input ended.
    member _.ReachedEOF = offset = -1
    /// The offset of the last character the DFA tokenizer reached.
    /// By "offset" we mean how many characters after the character
    /// stream's current position it is.
    member _.LastCharacterOffset = offset

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
                DFAResult.Create lastAcceptSym lastAcceptOfs ofs
        else
            let c = span.[ofs]
            let newDFA = oops.GetNextDFAState c currState
            if newDFA.IsOk then
                match states.[newDFA.Value].AcceptSymbol with
                | None -> tokenizeDFA_impl states oops input (ofs + 1) newDFA lastAcceptOfs lastAcceptSym span
                | Some _ as acceptSymbol -> tokenizeDFA_impl states oops input (ofs + 1) newDFA ofs acceptSymbol span
            else
                DFAResult.Create lastAcceptSym lastAcceptOfs ofs

    let private tokenizeDFA states oops (input: CharStream) =
        if input.TryExpandPastOffset 0 then
            tokenizeDFA_impl states oops input 0 DFAStateTag.InitialState 0 None input.CharacterBuffer
        else
            DFAResult.EOF

    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize (groups: ImmutableArray<_>) states oops fTransform fMessage (input: CharStream) =
        let fail msg = Message (input.CurrentPosition, msg) |> ParserError |> raise
        let rec groupLoop isNoiseGroup (groupStack: Group list) =
            match groupStack with
            | [] -> ()
            | currentGroup :: gs ->
                let dfaResult = tokenizeDFA states oops input
                if dfaResult.ReachedEOF then
                    // Input ended but the current group can be ended by a newline.
                    if currentGroup.IsEndedByNewline then
                        groupLoop isNoiseGroup gs
                    // Input ended unexpectedly.
                    else
                        fail <| ParseErrorType.UnexpectedEndOfInputInGroup currentGroup
                else
                    let ofs = dfaResult.LastCharacterOffset
                    // A new group begins that is allowed to nest into this one.
                    match dfaResult.FoundToken, dfaResult.Symbol with
                    | true, Choice3Of4(GroupStart(_, tokGroupIdx))
                        when currentGroup.Nesting.Contains tokGroupIdx ->
                            let g = groups.[int tokGroupIdx]
                            advance isNoiseGroup input ofs
                            groupLoop isNoiseGroup (g :: groupStack)
                    // A symbol is found that ends the current group.
                    | true, sym when currentGroup.IsEndedBy sym ->
                        match currentGroup.EndingMode with
                        | EndingMode.Closed -> advance isNoiseGroup input ofs
                        | EndingMode.Open -> ()
                        groupLoop isNoiseGroup gs
                    // The existing group is continuing.
                    | foundSymbol, _ ->
                        match currentGroup.AdvanceMode, foundSymbol with
                        | AdvanceMode.Token, true ->
                            advance isNoiseGroup input ofs
                        | AdvanceMode.Character, _ | _, false ->
                            advanceByOne isNoiseGroup input
                        groupLoop isNoiseGroup groupStack
        let rec tokenLoop() =
            let newToken term =
                let data = unpinSpanAndGenerateObject term fTransform input
                let theHolyToken = Token.Create input.LastTokenPosition term data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let dfaResult = tokenizeDFA states oops input
            // Input ends outside of a group.
            if dfaResult.ReachedEOF then
                input.CurrentPosition |> ParseMessage.EndOfInput |> fMessage
                None
            else
                let ofs = dfaResult.LastCharacterOffset
                match dfaResult.FoundToken, dfaResult.Symbol with
                // We are neither inside any group, nor a new one is going to start.
                // The easiest case. We advance the input, and return the token.
                | true, Choice1Of4 term ->
                    input.StartNewToken()
                    advance false input ofs
                    newToken term
                // We found noise outside of any group.
                // We discard it, unpin its characters, and proceed.
                | true, Choice2Of4 _noise ->
                    advance true input ofs
                    tokenLoop()
                // A new group just started. We will enter the group loop function.
                | true, Choice3Of4(GroupStart(_, tokGroupIdx)) ->
                    let g = groups.[int tokGroupIdx]
                    input.StartNewToken()
                    let isNoiseGroup = not g.IsTerminal
                    advance isNoiseGroup input ofs
                    groupLoop isNoiseGroup [g]
                    match g.ContainerSymbol with
                    // The group is a terminal. We return it.
                    | Choice1Of2 term -> newToken term
                    // The group had been noise all along. We discard it and move on.
                    | Choice2Of2 _ -> tokenLoop()
                // A group end symbol is encountered but outside of any group.
                | true, Choice4Of4 groupEnd ->
                    fail <| ParseErrorType.UnexpectedGroupEnd groupEnd
                // We found an unrecognized symbol while being outside a group. This is an error.
                | false, _ ->
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
