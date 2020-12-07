// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar

/// A token is an instance of a `Terminal`.
/// Tokens carry post-processed terminals,
/// as well as their position in the text.
type Token(position, symbol, data) =
    static let eofTerminal =
        // "EOF!" in ASCII.
        Terminal(0x454F4621u, "EOF")
    static let eofSentinel = obj()
    /// The `Position` of the token in the input string.
    member _.Position: Position = position
    /// The `Terminal` whose instance is this token.
    member _.Symbol: Terminal = symbol
    /// The object the `PostProcessor` created for this token.
    member _.Data: obj = data
    /// Whether the token signifies that input ended.
    /// When this property is set to true, the `Token.Symbol`
    /// and `Token.Data` properties have undefined values.
    member _.IsEOF = data = eofSentinel
    /// Creates a token that signifies the end of input at the given position.
    static member CreateEOF position = Token(position, eofTerminal, eofSentinel)
    override x.ToString() =
        if x.IsEOF then
            sprintf "%O (EOF)" position
        else
            sprintf "%O %O: \"%O\"" position symbol data

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

/// The type of an error the parser encountered.
[<RequireQualifiedAccess>]
type ParseErrorType =
    /// Unexpected end of input.
    | UnexpectedEndOfInput
    /// A character was not recognized.
    | LexicalError of char
    /// A symbol was read, while some others were expected.
    | SyntaxError of Expected: ExpectedSymbol Set * Actual: ExpectedSymbol
    /// A group did end, but outside of any group.
    | UnexpectedGroupEnd of Symbol: GroupEnd
    /// Unexpected end of input while being inside a group.
    | UnexpectedEndOfInputInGroup of Group: Group
    /// A custom error was raised by calling the `error` function
    /// or by throwing a `ParserApplicationException`.
    | UserError of Message: string
    override x.ToString() =
        match x with
        | UnexpectedEndOfInput -> "Unexpected end of input."
        | LexicalError x -> sprintf "Cannot recognize character '%c'." x
        | SyntaxError (expected, actual) when expected.Count = 1 ->
            sprintf "Found %O while expecting %O" actual expected.MinimumElement
        | SyntaxError (expected, actual) ->
            let expected = expected |> Seq.map string |> String.concat ", "
            sprintf "Found %O while expecting one of the following tokens: %s." actual expected
        | UnexpectedGroupEnd(GroupEnd name) -> sprintf "'%s' was encountered outside of any group." name
        | UnexpectedEndOfInputInGroup g -> sprintf "Unexpected end of input while being inside '%s'." g.Name
        | UserError x -> x

/// A parse error. It contains the position it was encountered and its type.
type ParserError = ParserError of Position: Position * ErrorType: ParseErrorType
    with
        override x.ToString() = match x with ParserError(pos, m) -> sprintf "%O %O" pos m
