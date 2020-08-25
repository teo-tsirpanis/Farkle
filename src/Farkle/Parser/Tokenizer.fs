// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammar
open Farkle.IO
open System

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

/// <summary>A class that breaks down the characters of a
/// <see cref="CharStream"/> into <see cref="Token"/>s.</summary>
/// <remarks>User code can inherit this class and implement additional
/// tokenizer logic by overriding the <see cref="GetNextToken"/>
/// method.</remarks>
type Tokenizer(grammar: Grammar) =

    let dfaStates = grammar.DFAStates
    let groups = grammar.Groups
    let oops = OptimizedOperations.Create grammar

    // Unfortunately we can't have ref structs on inner functions yet.
    let rec tokenizeDFA_impl (input: CharStream) ofs currState lastAcceptOfs lastAcceptSym (span: ReadOnlySpan<_>) =
        if ofs = span.Length then
            if input.TryExpandPastOffset(ofs) then
                tokenizeDFA_impl input ofs currState lastAcceptOfs lastAcceptSym input.CharacterBuffer
            else
                DFAResult.Create lastAcceptSym lastAcceptOfs ofs
        else
            let c = span.[ofs]
            let newDFA = oops.GetNextDFAState c currState
            if newDFA.IsOk then
                match dfaStates.[newDFA.Value].AcceptSymbol with
                | None -> tokenizeDFA_impl input (ofs + 1) newDFA lastAcceptOfs lastAcceptSym span
                | Some _ as acceptSymbol -> tokenizeDFA_impl input (ofs + 1) newDFA ofs acceptSymbol span
            else
                DFAResult.Create lastAcceptSym lastAcceptOfs ofs

    let tokenizeDFA (input: CharStream) =
        if input.TryExpandPastOffset 0 then
            tokenizeDFA_impl input 0 DFAStateTag.InitialState 0 None input.CharacterBuffer
        else
            DFAResult.EOF

    /// Returns the next token from the current position of a `CharStream`.
    /// A delegate to transform the resulting terminal is also given, as well
    /// as one that logs events.
    let tokenize transformer fMessage (input: CharStream) =
        // By returning unit the compiler does
        // not translate it to an FSharpTypeFunc.
        let fail msg = Message (input.CurrentPosition, msg) |> ParserException |> raise |> ignore
        let rec groupLoop isNoiseGroup (groupStack: Group list) =
            match groupStack with
            | [] -> ()
            | currentGroup :: gs ->
                let dfaResult = tokenizeDFA input
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
                            input.AdvancePastOffset(ofs, isNoiseGroup)
                            groupLoop isNoiseGroup (g :: groupStack)
                    // A symbol is found that ends the current group.
                    | true, sym when currentGroup.IsEndedBy sym ->
                        match currentGroup.EndingMode with
                        | EndingMode.Closed -> input.AdvancePastOffset(ofs, isNoiseGroup)
                        | EndingMode.Open -> ()
                        groupLoop isNoiseGroup gs
                    // The existing group is continuing.
                    | foundSymbol, _ ->
                        let ofsToAdvancePast =
                            match currentGroup.AdvanceMode, foundSymbol with
                            | AdvanceMode.Token, true -> ofs
                            | AdvanceMode.Character, _ | _, false -> 0
                        input.AdvancePastOffset(ofsToAdvancePast, isNoiseGroup)
                        groupLoop isNoiseGroup groupStack
        let rec tokenLoop() =
            let newToken term =
                let data = input.FinishNewToken term transformer
                let theHolyToken = Token.Create input.LastTokenPosition term data
                theHolyToken |> ParseMessage.TokenRead |> fMessage
                Some theHolyToken
            let dfaResult = tokenizeDFA input
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
                    input.AdvancePastOffset(ofs, false)
                    newToken term
                // We found noise outside of any group.
                // We discard it, unpin its characters, and proceed.
                | true, Choice2Of4 _noise ->
                    input.AdvancePastOffset(ofs, true)
                    tokenLoop()
                // A new group just started. We will enter the group loop function.
                | true, Choice3Of4(GroupStart(_, tokGroupIdx)) ->
                    let g = groups.[int tokGroupIdx]
                    input.StartNewToken()
                    let isNoiseGroup = not g.IsTerminal
                    input.AdvancePastOffset(ofs, isNoiseGroup)
                    groupLoop isNoiseGroup [g]
                    match g.ContainerSymbol with
                    // The group is a terminal. We return it.
                    | Choice1Of2 term -> newToken term
                    // The group had been noise all along. We discard it and move on.
                    | Choice2Of2 _ -> tokenLoop()
                // A group end symbol is encountered but outside of any group.
                | true, Choice4Of4 groupEnd ->
                    fail <| ParseErrorType.UnexpectedGroupEnd groupEnd
                    None
                // We found an unrecognized symbol while being outside a group. This is an error.
                | false, _ ->
                    let errorPos = input.GetPositionAtOffset ofs
                    let errorType =
                        let span = input.CharacterBuffer
                        if ofs = span.Length then
                            ParseErrorType.UnexpectedEndOfInput
                        else
                            ParseErrorType.LexicalError span.[ofs]
                    Message(errorPos, errorType)
                    |> ParserException
                    |> raise
        tokenLoop()

    /// The `OptimizedOperations` object of the grammar of this tokenizer.
    /// Used to avoid explicitly passing it to the LALR parser.
    member internal _.OptimizedOperations = oops

    /// <summary>Gets the next <see cref="Token"/>
    /// from a <see cref="CharStream"/>.</summary>
    /// <remarks>Custom inheritors that want to defer to Farkle's
    /// tokenizer can do it by calling the base method.</remarks>
    /// <param name="transformer">This parameter is used for the
    /// post-processor. It should be passed to the base method if needed.</param>
    /// <param name="fMessage">A function that is used for logging parsing events.</param>
    /// <param name="input">The <see cref="CharStream"/> whose characters will be processed</param>
    /// <returns>The next token, or <c>None</c> if input ended.</returns>
    abstract GetNextToken: transformer: ITransformer<Terminal> * fMessage: (ParseMessage -> unit) * input: CharStream -> Token option
    default _.GetNextToken(transformer, fMessage, input) =
        tokenize transformer fMessage input

[<Sealed>]
/// A sealed dummy descendant of `Tokenizer`.
/// It is used to help the runtime to maybe
/// do its devirtualization shenanigans.
type internal DefaultTokenizer(grammar) = inherit Tokenizer(grammar)
