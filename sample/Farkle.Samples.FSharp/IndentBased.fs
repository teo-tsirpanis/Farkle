// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Samples.FSharp.IndentBased

open Farkle
open Farkle.Builder
open Farkle.Grammars
open Farkle.Parser
open Farkle.Parser.Tokenizers
open System
open System.Collections.Generic

// This file contains a grammar for IndentCode, a relatively simple
// indent-based language. The implementation of this grammar and the
// custom tokenizer it requires serves as an example to show how to
// use a custom tokenizer and virtual terminals. To give you an example
// of this language, let's take a look at the following file:
// A
//     B
//     C
//         D
// It will correspond to Block [Line "A"; Block [Line "B"; Line "C"; Block [Line "D"]]].
// Indentation is done using spaces. Empty lines are permitted. Tabs are not supported
// and might cause problems if used instead of spaces.
type IndentCode =
    | Line of string
    | Block of IndentCode list

[<Literal>]
let BlockStartSpecialName = "__IndentCode_BlockStart"
[<Literal>]
let BlockEndSpecialName = "__IndentCode_BlockEnd"

let grammarBuilder =
    // We create two virtual terminals signifying when indentation goes up and down.
    // These virtual terminals don't have a regex associated with them; Farkle will
    // never find them in text on its own. Instead we will emit them from the custom
    // tokenizer we will write below.
    // These terminals are able to be retrieved by a special name. Unlike regular symbol
    // names, a special name must be unique in a grammar.
    let blockStart = Terminal.Virtual(BlockStartSpecialName, TerminalOptions.SpecialName).Rename("Block Start")
    let blockEnd = Terminal.Virtual(BlockEndSpecialName, TerminalOptions.SpecialName).Rename("Block End")

    let line =
        // Exclude leading spaces from line tokens.
        Regex.regexString "[^\r\n ][^\r\n]*"
        |> terminal "Line" (T(fun _ data -> data.ToString() |> Line))

    let indentCodeBody = nonterminal "IndentCode Body"
    let indentCodeBlock = nonterminal "IndentCode Block"

    indentCodeBody.SetProductions(
        !@ line |> asProduction,
        !% blockStart .>>. indentCodeBlock .>> blockEnd => Block)
    indentCodeBlock.SetProductions(
        !@ indentCodeBody .>> newline .>>. indentCodeBlock => (fun x xs -> x :: xs),
        !@ indentCodeBody => List.singleton,
        empty =% [])

    indentCodeBody
    // Redefine whitespace to not include tabs.
    |> _.AutoWhitespace(false)
    |> _.AddNoiseSymbol("Whitespace", Regex.regexString " +")
    // Ignore repeated newlines.
    |> _.NewLineIsNoisy(true)
    |> _.WithGrammarName("IndentCode")

type IndentCodeTokenizerState() =
    let indentLevels = Stack<int>()
    // This stack holds the indentation levels of each block.
    member _.IndentLevels = indentLevels
    // We use this guard variable to detect
    // and fail on cases like the following:
    // A
    //     B
    //    C
    member val IsExitingBlock = false with get, set

// And here is our tokenizer. Its constructor accepts a grammar.
// Farkle will automatically pass the IndentCode's grammar.
type IndentCodeTokenizer(grammar: IGrammarProvider) as this =
    inherit Tokenizer<char>()

    static let stateKey = obj()

    // These two fields hold our virtual terminals. They have
    // nothing to do with the virtual terminals we declared above;
    // they were designtime Farkles and this one is a Farkle.Grammars.Terminal.
    // Caching them instead of calling Grammar.GetTerminalByName
    // is a good practice for performance and clarity reasons.
    let blockStart = grammar.GetTokenSymbolFromSpecialName BlockStartSpecialName
    let blockEnd = grammar.GetTokenSymbolFromSpecialName BlockEndSpecialName

    // This function slices a character buffer until it ends or the line changes.
    // The line end character is included.
    let getNextLine (buffer: ReadOnlySpan<char>) =
        // We try to find either a carriage return or a line feed.
        match buffer.IndexOfAny("\r\n".AsSpan()) with
        // If we didn't find it, it might mean that only part of
        // the line is loaded in memory. We simply return the entire buffer.
        | -1 -> buffer
        // If a new line character was found, we return the buffer,
        // sliced to contain all characters including the new line.
        | newLineIdx -> buffer.Slice(0, newLineIdx + 1)

    // We use this method to get or create our state object.
    // Starting with Farkle 7, tokenizers are stateless and reused by many parser operations.
    // We have to use the ParserState object to store any state, which provides a key-value
    // store. We have declared above a dummy object to serve as a key for this tokenizer's
    // state. The reason for this is to prevent our state from conflicting with potentially
    // other tokenizers' states.
    let getOrCreateState (state: ParserState byref) =
        match state.TryGetValue(stateKey) with
        | true, value -> value :?> _
        | false, _ ->
            let x = IndentCodeTokenizerState()
            state.SetValue(stateKey, x)
            x

    let impl (input: ParserInputReader<_> byref) =
        let state = getOrCreateState &input.State
        let indentLevels = state.IndentLevels
        if not input.IsEndOfInput then
            // Checking for the indentation makes sense only
            // when we are at the start of a line.
            if input.State.CurrentPosition.Column = 1 then
                // Get the next line from the input.
                let nextLine = getNextLine input.RemainingCharacters
                // Trim the line to remove any leading spaces.
                let nextLineTrimmed = nextLine.TrimStart(' ')
                // We find the indentation level we are at by calculating
                // how many spaces TrimStart took from our line.
                let currentIndentLevel = nextLine.Length - nextLineTrimmed.Length
                // If the line is empty or has only spaces, there are two possible cases:
                if nextLineTrimmed.IsEmpty then
                    // If there are more characters after the spaces, we cannot make
                    // a decision because there might be more spaces following and
                    // the indentation level might change. By calling SuspendTokenizer
                    // we interrupt the tokenizer chain and yield from the parser code,
                    // in order to read more characters. When the tokenizer chain runs
                    // again, it will continue from here.
                    if not input.IsFinalBlock then
                        input.SuspendTokenizer(this)
                    // If there aren't, input has reached its end. We let Farkle's
                    // tokenizer consume the last characters, and when we return we
                    // will emit any block end tokens that are needed.
                    // Either way we return no token for now.
                    ValueNone
                // If the line consisted only of spaces, defer to Farkle's tokenizer
                // to change the line. We will then continue from the start of the
                // next line.
                elif nextLineTrimmed[0] = '\n' || nextLineTrimmed[0] = '\r' then
                    ValueNone
                // If we are outside of any block or our indentation
                // level is bigger than our current block's, it means
                // that we are about to enter a new block.
                elif indentLevels.Count = 0 || currentIndentLevel > indentLevels.Peek() then
                    // If we are in the process of exiting a block, we can't enter a new one.
                    if state.IsExitingBlock then
                        // We use the FailAtOffset extension method to throw a
                        // ParserApplicationException after the indentation. This
                        // type of exception is specially handled by Farkle to
                        // return just an error message without a stack trace.
                        // The message is borrowed from Python.
                        input.FailAtOffset(currentIndentLevel, "unindent does not match any outer indentation level")
                    // We push this line's indentation level to our stack.
                    indentLevels.Push currentIndentLevel
                    // We could have consumed the spaces and tell the input reader to
                    // not show them again but let's not do that, to demonstrate how
                    // the tokenizers can work together. Farkle's tokenizer will consume
                    // the spaces, this tokenizer will enter again and quickly leave because
                    // we are not at the start of a line, and then Farkle's tokenizer will
                    // emit the line token. Furthermore letting this tokenizer consume the
                    // characters would pose problems for more complex scenarios like comments.
                    // input.Consume currentIndentLevel
                    state.IsExitingBlock <- false
                    // And we emit a block start token. The null parameter is the token's data.
                    // Virtual terminals are always untyped so the token's data is null.
                    TokenizerResult.CreateSuccess(blockStart, null, input.State.CurrentPosition)
                    |> ValueSome
                // If our indentation level is equal to our block's,
                // we are staying at the same block we are.
                elif currentIndentLevel = indentLevels.Peek() then
                    state.IsExitingBlock <- false
                    ValueNone
                // And if our indentation level is smaller than our
                // block's, it means that we are exiting that block.
                else
                    // We pop the indentation level from the stack.
                    indentLevels.Pop() |> ignore
                    // With the following line we forbid entering new blocks
                    // until we encounter a line that is at an existing block.
                    // It prevents things like the example above from happening.
                    state.IsExitingBlock <- true
                    // And finally we return a block end token.
                    // We must not call input.Consume here.
                    // The next time this method is called, the line's indentation
                    // level will be considered again, allowing stuff like this:
                    // A
                    //     B
                    //         C
                    // D
                    // to emit two block end tokens before processing line D's content.
                    TokenizerResult.CreateSuccess(blockEnd, null, input.State.CurrentPosition)
                    |> ValueSome
            else
                // If we are not at the beginning of a line we defer to
                // Farkle's tokenizer to process the rest of the line.
                ValueNone
        else
            // If we are at the end of input we have
            // to end all blocks we are currently into.
            // The parser expects one block end token for
            // each block start.
            match indentLevels.TryPop() with
            | true, _ ->
                // By suspending the tokenizer, we make sure the next time the
                // tokenizer chain runs, it will continue from here, to emit any
                // additional block end tokens.
                input.SuspendTokenizer(this)
                TokenizerResult.CreateSuccess(blockEnd, null, input.State.CurrentPosition)
                |> ValueSome
            // If we don't have any more blocks to end, we are done. Farkle's
            // tokenizer will return no result as well, and input will end.
            | false, _ -> ValueNone

    // This is a stub method that calls impl and converts the ValueOption
    // to C#'s try pattern.
    override _.TryGetNextToken(input, _, token) =
        match impl &input with
        | ValueSome x ->
            token <- x
            true
        | ValueNone ->
            false

// We build our grammar almost as usual.
let parser =
    grammarBuilder
    |> GrammarBuilder.build
    // To tell Farkle to use our custom tokenizer with our
    // parser, we have to create a tokenizer chain. In a chain,
    // each tokenizer is invoked in order, until one returns a
    // result (either success of failure). If all tokenizers do
    // not return a result, the parser will yield execution and
    // will return after being given more characters to parse,
    // or signal the end of input if no more input is available.
    // The chain is comprised of two tokenizers; our IdentCodeTokenizer,
    // and the parser's pre-existing tokenizer, which is Farkle's built-in one.
    |> CharParser.withTokenizerChain [TokenizerFactory (fun grammar -> IndentCodeTokenizer(grammar)); DefaultTokenizer]
