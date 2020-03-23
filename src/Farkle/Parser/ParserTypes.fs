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

/// An action of the parser.
[<RequireQualifiedAccess>]
type ParseMessage =
    /// Input ended.
    | EndOfInput of Position
    /// A token was read.
    | TokenRead of Token
    /// A rule was reduced.
    | Reduction of Production
    /// The parser shifted to a different LALR state.
    | Shift of uint32
    override x.ToString() =
        match x with
        | EndOfInput pos -> sprintf "%O Input ended" pos
        | TokenRead x -> sprintf "%O Token read: %O (%s)" x.Position x x.Symbol.Name
        | Reduction x -> sprintf "Reduction: %O" x
        | Shift x -> sprintf "Shift: %d" x

/// An error the parser encountered.
[<RequireQualifiedAccess>]
type ParseErrorType =
    /// A character was not recognized.
    /// Specifying the null `U+0000` character
    /// means that input ended while tokenizing.
    | LexicalError of char
    /// A symbol was read, while some others were expected.
    | SyntaxError of expected: ExpectedSymbol Set * actual: ExpectedSymbol
    /// A group was read, but cannot be nested on top of the previous one.
    | CannotNestGroups of Group * Group
    /// A group did end, but outside of any group.
    | UnexpectedGroupEnd of GroupEnd
    /// Unexpected end of input while being inside a group.
    | UnexpectedEndOfInput of Group
    override x.ToString() =
        match x with
        | LexicalError '\000' -> "Unexpected end of input."
        | LexicalError x -> sprintf "Cannot recognize character '%c'." x
        | SyntaxError (expected, actual) ->
            let expected = expected |> Seq.map string |> String.concat ", "
            sprintf "Found %O, while expecting one of the following tokens: %s." actual expected
        | CannotNestGroups(g1, g2) -> sprintf "Group '%s' cannot be nested inside '%s'" g1.Name g2.Name
        | UnexpectedGroupEnd ge -> sprintf "'%s' was encountered outside of any group." ge.Name
        | UnexpectedEndOfInput g -> sprintf "Unexpected end of input while being inside '%s'." g.Name

/// A log message that contains a position it was encountered.
type Message<'a> = Message of Position * 'a
    with
        override x.ToString() = let (Message(pos, m)) = x in sprintf "%O %O" pos m

/// An exception to be thrown when parsing goes wrong.
/// It is thrown by the `Parser` and `Tokenizer` APIs, and caught by the `RuntimeFarkle`.
exception ParseError of ParseErrorType Message
