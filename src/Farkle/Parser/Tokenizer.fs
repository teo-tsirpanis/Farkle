// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar
open Farkle.IO
open System
open System.Diagnostics

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

[<AbstractClass>]
/// <summary>A class that breaks down the characters of a
/// <see cref="CharStream"/> into <see cref="Token"/>s.</summary>
/// <remarks>User code can inherit this class and implement additional
/// tokenizer logic by overriding the <see cref="GetNextToken"/>
/// method.</remarks>
/// <seealso cref="DefaultTokenizer"/>
type Tokenizer() =
    /// <summary>Gets the next <see cref="Token"/>
    /// from a <see cref="CharStream"/>.</summary>
    /// <param name="transformer">This parameter is used for the
    /// post-processor. It should be passed to the base method if called.</param>
    /// <param name="input">The <see cref="CharStream"/> whose characters will be processed.</param>
    abstract GetNextToken: transformer: ITransformer<Terminal> * input: CharStream -> Token

/// <summary>Farkle's default tokenizer, powered by a DFA.</summary>
/// <remarks>Custom tokenizers are recommended to inherit
/// this class to still have access to Farkle's tokenizer
/// through the base <see cref="GetNextToken"/> method.</remarks>
type DefaultTokenizer(grammar: Grammar) =
    inherit Tokenizer()

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
                let newDFA = newDFA.Value
                match dfaStates.[newDFA].AcceptSymbol with
                | None -> tokenizeDFA_impl input (ofs + 1) newDFA lastAcceptOfs lastAcceptSym span
                | Some _ as acceptSymbol -> tokenizeDFA_impl input (ofs + 1) newDFA ofs acceptSymbol span
            else
                DFAResult.Create lastAcceptSym lastAcceptOfs ofs

    let tokenizeDFA (input: CharStream) =
        if input.TryExpandPastOffset 0 then
            tokenizeDFA_impl input 0 DFAStateTag.InitialState 0 None input.CharacterBuffer
        else
            DFAResult.EOF

    /// <inheritdoc/>
    override _.GetNextToken(transformer, input) =
        // By returning unit the compiler does
        // not translate it to an FSharpTypeFunc.
        let fail msg = ParserError(input.CurrentPosition, msg) |> ParserException |> raise |> ignore
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
            let newToken (term: Terminal) =
                let data =
                    try
                        input.CreateToken term transformer
                    with
                    | :? ParserException
                    | :? ParserApplicationException -> reraise()
                    | e -> PostProcessorException(term, e) |> raise
                let theHolyToken = Token(input.TokenStartPosition, term, data)
                theHolyToken
            let dfaResult = tokenizeDFA input
            // Input ends outside of a group.
            if dfaResult.ReachedEOF then
                Token.CreateEOF input.CurrentPosition
            else
                Debug.Assert(input.CurrentPosition.Index = input.TokenStartPosition.Index,
                    "The character stream's current position and starting position are not the same.")
                let ofs = dfaResult.LastCharacterOffset
                match dfaResult.FoundToken, dfaResult.Symbol with
                // We are neither inside any group, nor a new one is going to start.
                // The easiest case. We advance the input, and return the token.
                | true, Choice1Of4 term ->
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
                    // This line will not be executed.
                    Token.CreateEOF input.CurrentPosition
                // We found an unrecognized symbol while being outside a group. This is an error.
                | false, _ ->
                    let errorPos = input.GetPositionAtOffset ofs
                    let errorType =
                        let span = input.CharacterBuffer
                        if ofs = span.Length then
                            ParseErrorType.UnexpectedEndOfInput
                        else
                            ParseErrorType.LexicalError span.[ofs]
                    ParserError(errorPos, errorType)
                    |> ParserException
                    |> raise
        tokenLoop()
