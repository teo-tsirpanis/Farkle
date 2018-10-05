// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar

/// An internal error. These errors are known errors a program might experience.
/// They could occur by manipulating the parser internal state, which is _impossible_ from the public API.
/// Another possible way of manifestation is creating deliberately faulty `Grammar`s.
type ParseInternalError =
    /// After a reduction, the next LALR action should be a `Goto` one.
    /// But it's not.
    | GotoNotFoundAfterReduction of Production * LALRState
    /// The LALR stack is empty; it should never be.
    | LALRStackEmpty
    /// The LALR stack did not have a `Reduction` on its top when the parser accepted the input.
    | ReductionNotFoundOnAccept

[<RequireQualifiedAccess>]
type LALRResult =
    | Accept of obj
    | Shift of uint32
    | Reduce of Production
    | SyntaxError of expected: Symbol list * actual: Symbol
    | InternalError of ParseInternalError

/// An action of the parser.
type ParseMessageType =
    /// A token was read.
    | TokenRead of Token
    /// A rule was reduced.
    | Reduction of Production
    /// The parser shifted to a different LALR state.
    | Shift of uint32
    override x.ToString() =
        match x with
        | TokenRead x -> sprintf "Token read: \"%O\" (%s)" x x.Symbol.Name
        | Reduction x -> sprintf "Rule reduced: %O" x
        | Shift x -> sprintf "The parser shifted to state %d" x

/// A log message from a `Parser`, and the position it was encountered.
type ParseMessage = ParseMessage of Position * ParseMessageType
    with
        override x.ToString() = match x with | ParseMessage (pos, mt) ->  sprintf "%O %O" pos mt
        /// Creates a parse message at the default position.
        static member CreateSimple mt = ParseMessage (Position.initial, mt)

/// An error
type ParseErrorType =
    /// The file containing the input does not exist.
    | InputFileNotExist of string
    /// A character was not recognized.
    | LexicalError of char
    /// A symbol was read, while some others were expected.
    | SyntaxError of expected: Symbol list * actual: Symbol
    /// Unexpected end of input.
    | GroupError
    /// Internal error. This is a bug.
    | InternalError of ParseInternalError
    override x.ToString() =
        match x with
        | InputFileNotExist x -> sprintf "File \"%s\" does not exist" x
        | LexicalError x -> sprintf "Cannot recognize token: %c" x
        | SyntaxError (expected, actual) ->
            let expected = expected |> List.map string |> String.concat ", "
            sprintf "Found %O, while expecting one of the following tokens: %O" actual expected
        | GroupError -> "Unexpected end of input"
        | InternalError x -> sprintf "Internal error: %O. This is most probably a bug. If you see this error, please file an issue on GitHub." x

/// A log message from a `Parser`, and the position it was encountered.
[<RequireQualifiedAccess>]
type ParseError = ParseError of Position * ParseErrorType
    with
        override x.ToString() = match x with | ParseError (pos, mt) ->  sprintf "%O %O" pos mt


[<RequireQualifiedAccess>]
/// The result of a tokenizer step.
type TokenizerResult =
    /// A token was read.
    | TokenRead of Token
    /// An unknown character was encountered at the given position.
    | LexicalError of char * Position
    /// The input ended while inside a lexical group.
    | GroupError of Position
    member x.Position =
        match x with
        | TokenRead {Position = pos} | LexicalError (_, pos) | GroupError pos-> pos

/// A tokenizer. What is it actually? A sequence of tokens (or some other information).
type Tokenizer = TokenizerResult seq

/// A LALR parser. It takes a `Token`, and gives an `LALRResult`.
/// It is a stateful operation; the type of this state
/// is abstracted from the rest of the parser.
type LALRParser = LALRParser of (Token -> (LALRResult * LALRParser))
