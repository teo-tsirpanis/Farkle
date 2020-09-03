// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar

/// A token is an instance of a `Terminal`.
/// Tokens carry post-processed terminals, as well as their position within the text file.
type Token =
    {
        /// The `Terminal` whose instance is this token.
        Symbol: Terminal
        /// The `Position` of the token in the input string.
        Position: Position
        /// The actual content of the token.
        Data: obj
    }
    with
        /// A shortcut for creating a token.
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        override x.ToString() = if isNull x.Data then "" else sprintf "\"%O\"" x.Data

[<RequireQualifiedAccess>]
/// A symbol that was expected at the location of a syntax error.
type ExpectedSymbol =
    /// A terminal was expected.
    | Terminal of Terminal
    /// The input was expected to end.
    | EndOfInput
    override x.ToString() =
        match x with
        | Terminal x -> x.ToString()
        | EndOfInput -> "(EOF)"

/// An error the parser encountered.
[<RequireQualifiedAccess>]
type ParseErrorType =
    /// Unexpected end of input.
    | UnexpectedEndOfInput
    /// A character was not recognized.
    | LexicalError of char
    /// A symbol was read, while some others were expected.
    | SyntaxError of expected: ExpectedSymbol Set * actual: ExpectedSymbol
    /// A group did end, but outside of any group.
    | UnexpectedGroupEnd of GroupEnd
    /// Unexpected end of input while being inside a group.
    | UnexpectedEndOfInputInGroup of Group
    /// A custom error was raised by calling the `error` function
    /// or by throwing a `ParserApplicationException`.
    | UserError of string
    override x.ToString() =
        match x with
        | UnexpectedEndOfInput -> "Unexpected end of input."
        | LexicalError x -> sprintf "Cannot recognize character '%c'." x
        | SyntaxError (expected, actual) ->
            let expected = expected |> Seq.map string |> String.concat ", "
            sprintf "Found %O, while expecting one of the following tokens: %s." actual expected
        | UnexpectedGroupEnd ge -> sprintf "'%s' was encountered outside of any group." ge.Name
        | UnexpectedEndOfInputInGroup g -> sprintf "Unexpected end of input while being inside '%s'." g.Name
        | UserError x -> x

/// A log message that contains the position it was encountered.
type Message<'a> = Message of Position * 'a
    with
        override x.ToString() = let (Message(pos, m)) = x in sprintf "%O %O" pos m

/// <summary>An exception to be thrown when parsing goes wrong.</summary>
/// <remarks>It is thrown by the <see cref="LALRParser"/> and <see cref="Tokenizer"/>
/// APIs and transparently caught by the <see cref="RuntimeFarkle"/>.</remarks>
exception ParserException of ParseErrorType Message
