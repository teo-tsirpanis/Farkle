// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Samples.IndentBased

open Farkle
open Farkle.Builder
open Farkle.IO
open Farkle.Parser
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

let designtime =
    // We create two virtual terminals signifying when indentation goes up and down.
    // These virtual terminals don't have a regex associated with them; Farkle will
    // never find them in text on its own. Instead we will emit them from the custom
    // tokenizer we will write below.
    let blockStart = virtualTerminal "Block Start"
    let blockEnd = virtualTerminal "Block End"

    let line =
        Regex.regexString "[^\r\n]+"
        |> terminal "Line" (T(fun _ data -> data.ToString() |> Line))

    let nlOpt = nonterminalU "NewLine Optional"
    nlOpt.SetProductions(!% nlOpt .>> newline, empty)

    let nl = nonterminalU "NewLine"
    nl.SetProductions(!% nl .>> newline, !% newline)

    let indentCodeBody = nonterminal "IndentCode Body"
    let indentCodeBlock = nonterminal "IndentCode Block"

    indentCodeBody.SetProductions(
        !@ line => id,
        !% blockStart .>>. indentCodeBlock .>> blockEnd => Block)
    indentCodeBlock.SetProductions(
        !@ indentCodeBody .>> nl .>>. indentCodeBlock => (fun x xs -> x :: xs),
        !@ indentCodeBody => List.singleton,
        empty =% [])

    // To allow stray newlines everywhere, we have to place nlOpt terminals
    // ourselves. Remember, if the newline designtime Farkle is present,
    // Farkle does NOT ignore newlines.
    "IndentCode" ||= [!% nlOpt .>>. indentCodeBody .>> nlOpt => id]

// And here is our tokenizer. Its constructor accepts a grammar.
// Farkle will automatically pass the IndentCode's grammar.
// Tokenizers are generally stateless; a brand-new tokenizer will
// be created for each time Farkle parses IndentCode text.
type IndentCodeTokenizer(grammar) =
    // The recommended way to define custom tokenizer classes is to
    // inherit the DefaultTokenizer. It allows us to defer to Farkle's
    // tokenizer whenever we need it; it's a good practice to do only
    // what you must in your tokenizer and let Farkle handle the rest.
    inherit DefaultTokenizer(grammar)

    // This stack holds the indentation levels of each block.
    let indentLevels = Stack()

    // We use this guard variable to detect
    // and fail on cases like the following:
    // A
    //     B
    //    C
    let mutable exitingBlock = false

    // These two fields hold our virtual terminals. They have
    // nothing to do with the virtual terminals we declared above;
    // they were designtime Farkles and this one is a Farkle.Grammar.Terminal.
    // Caching them instead of calling Grammar.GetTerminalByName
    // is a good practice for performance and clarity reasons.
    let blockStart = grammar.GetTerminalByName "Block Start"
    let blockEnd = grammar.GetTerminalByName "Block End"

    // This function reads the next full line from our input character stream.
    let rec getNextLine (input: CharStream) =
        // CharacterBuffer returns a read-only span of all
        // available characters that we have not yet read.
        let buffer = input.CharacterBuffer
        // We try to find either a carriage return or a line feed.
        match buffer.IndexOfAny("\r\n".AsSpan()) with
        // If we didn't find it, it might mean that only part of
        // the line is loaded in memory. We use the TryExpandPastOffset
        // method to load more characters further than those already in
        // memory. If it returns true, it means that there are more, so we try again.
        | -1 when input.TryExpandPastOffset buffer.Length -> getNextLine input
        // If the method returned false it means that input had actually ended
        // and there were no new line characters. We simply return the entire buffer.
        | -1 -> buffer
        // If a new line character was found, we return the stream's
        // buffer, sliced to contain all characters before the new line.
        | newLineIdx -> buffer.Slice(0, newLineIdx)

    // The GetNextToken method is where our tokenizer does the real work.
    // It takes the terminals' transformer (usually not used by custom
    // tokenizers), and the character stream containing our input.
    override this.GetNextToken(transformer, input) =
        // By calling TryExpandPastOffset with a value
        // of 0 we check whether input ended or not.
        if input.TryExpandPastOffset 0 then
            // Checking for the indentation makes sense only
            // when we are at the start of a line.
            if input.CurrentPosition.Column = 1UL then
                let nextLine = getNextLine input
                let nextLineTrimmed = nextLine.TrimStart(' ')
                // We find the indentation level we are by calculating
                // how many spaces TrimStart took from our line.
                let currentIndentLevel = nextLine.Length - nextLineTrimmed.Length

                // If the line is empty or has only spaces, we
                // ignore it and don't consider indentation levels.
                if nextLineTrimmed.IsEmpty then
                    // We defer to Farkle's tokenizer to get the next token.
                    // It will ignore any spaces at the beginning. Even on
                    // line-based grammars, Farkle ignores new lines.
                    let token = base.GetNextToken(transformer, input)
                    // But if Farkle's tokenizer reports that input ended, we have
                    // to give our own tokenizer a second chance to see the EOF
                    // and start issuing Block Ends. Such scenario will only occur
                    // if the last line has whitespace; Farkle would skip it, see
                    // that input ended, and return EOF. Also, don't worry about the
                    // recursive method call; it won't be recursively called again.
                    if token.IsEOF then
                        this.GetNextToken(transformer, input)
                    else
                        token
                // If we are outside of any block or our indentation
                // level is bigger than our current block's, it means
                // that we are about to enter a new block.
                elif indentLevels.Count = 0 || currentIndentLevel > indentLevels.Peek() then
                    // If we are in the process of exiting a block, we can't enter a new one.
                    if exitingBlock then
                        // The error function will throw a ParserApplicationException
                        // which is specially handled by Farkle to return just an error
                        // message without a stack trace. The message is borrowed from Python.
                        error "unindent does not match any outer indentation level"
                    // We push this line's indentation level to our stack.
                    indentLevels.Push currentIndentLevel
                    // We tell the character stream to not show us these spaces again.
                    input.AdvanceBy currentIndentLevel
                    exitingBlock <- false
                    // And we return a block start token. The null
                    // at the last parameter is the token's data.
                    // Virtual terminals are always untyped so we return null.
                    Token(input.CurrentPosition, blockStart, null)
                // If our indentation level is equal to our block's,
                // we are staying at the same block we are.
                elif currentIndentLevel = indentLevels.Peek() then
                    exitingBlock <- false
                    // We have nothing else to do and defer to
                    // Farkle's tokenizer to get our line token.
                    base.GetNextToken(transformer, input)
                // And if our indentation level is smaller than our
                // block's, it means that we are exiting that block.
                else
                    // We pop the indentation level from the stack.
                    indentLevels.Pop() |> ignore
                    // With the following line we forbid entering new blocks
                    // until we encounter a line that is at an existing block.
                    // It prevents things like the example above from happening.
                    exitingBlock <- true
                    // And finally we return a block end token.
                    // Note that we did not call input.AdvanceTo like before.
                    // The next time this method is called, the line's indentation
                    // level will be considered again, allowing stuff like this:
                    // A
                    //     B
                    //         C
                    // D
                    // to emit two block end tokens before processing line D's content.
                    Token(input.CurrentPosition, blockEnd, null)
            else
                // If we are not at the beginning of a line we defer to
                // Farkle's tokenizer to process the rest of the line.
                base.GetNextToken(transformer, input)
        else
            // If we are at the end of the file we have
            // to end all blocks we are currently into.
            // The parser expects one block end token for
            // each block start.
            match indentLevels.TryPop() with
            | true, _ ->
                Token(input.CurrentPosition, blockEnd, null)
            | false, _ ->
                // Once the indent level stack is empty, we defer to Farkle's
                // tokenizer for one last time to officialy state that input ended.
                base.GetNextToken(transformer, input)

// We build our designtime Farkle almost as usual.
let runtime =
    designtime
    |> RuntimeFarkle.build
    // To tell Farkle to use our custom tokenizer with our
    // designtime Farkle, we use the changeTokenizer function.
    // It takes a function that gets a grammar and returns a
    // tokenizer. In our case that function is our tokenizer's constructor.
    |> RuntimeFarkle.changeTokenizer IndentCodeTokenizer
