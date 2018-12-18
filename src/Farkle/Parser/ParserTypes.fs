// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar

/// An internal error. These errors are known errors a program might experience.
/// They could occur by manipulating the parser internal state, which is _impossible_ from the public API.
/// Another possible way of manifestation is creating deliberately faulty `Grammar`s.
/// The `ToString()` method is not overriden, because users don;t need to know about them.
type InternalError =
    /// After a reduction, a corresponding GOTO action should have been found but wasn't.
    | GotoNotFoundAfterReduction of Production * LALRState
    /// The LALR stack is empty; it should never be.
    | LALRStackEmpty
    /// The LALR table says to shift when input ends.
    | ShiftOnEOF
    /// The LALR stack did not have a `Reduction` on its top when the parser accepted the input.
    | ReductionNotFoundOnAccept
    /// The post-processor had a problem fusing the tokems of a production.
    | FuseError of Production

[<RequireQualifiedAccess>]
/// A symbol that was expected at the location of a syntax error.
type ExpectedSymbol =
    /// A terminal was expected.
    | Terminal of Terminal
    /// A nonterminal was expected.
    | Nonterminal of Nonterminal
    /// The input was expected to end.
    | EndOfInput
    override x.ToString() =
        match x with
        | Terminal x -> x.ToString()
        | Nonterminal x -> x.ToString()
        | EndOfInput -> "(EOF)"

/// An action of the parser.
type ParseMessageType =
    /// A token was read.
    | TokenRead of Token option
    /// A rule was reduced.
    | Reduction of Production
    /// The parser shifted to a different LALR state.
    | Shift of uint32
    override x.ToString() =
        match x with
        | TokenRead None -> "Input ended"
        | TokenRead (Some x) -> sprintf "Token read: \"%O\" (%s)" x x.Symbol.Name
        | Reduction x -> sprintf "Rule reduced: %O" x
        | Shift x -> sprintf "The parser shifted to state %d" x

/// An error the parser encountered.
[<RequireQualifiedAccess>]
type ParseErrorType =
    /// A character was not recognized.
    | LexicalError of char
    /// A symbol was read, while some others were expected.
    | SyntaxError of expected: ExpectedSymbol Set * actual: ExpectedSymbol
    /// A group was read, but cannot be nested on top of the previous one.
    | CannotNestGroups of Group * Group
    /// A group did end, but outside of any group.
    | UnexpectedGroupEnd of GroupEnd
    /// Unexpected end of input while being inside a group.
    | UnexpectedEndOfInput of Group
    /// Internal error. This is a bug.
    | InternalError of InternalError
    override x.ToString() =
        match x with
        | LexicalError x -> sprintf "Cannot recognize character: %c" x
        | SyntaxError (expected, actual) ->
            let expected = expected |> Seq.map string |> String.concat ", "
            sprintf "Found %O, while expecting one of the following tokens: %O" actual expected
        | CannotNestGroups(g1, g2) -> sprintf "Group %O cannot be nested inside %O" g1 g2
        | UnexpectedGroupEnd ge -> sprintf "%O was encountered outside of any group." ge
        | UnexpectedEndOfInput g -> sprintf "Unexpected end of input while being inside a %s." g.Name
        | InternalError x -> sprintf "Internal error: %A. This is most probably a bug. If you see this error, please file an issue on GitHub." x

/// A log message that contains a position it was encountered.
type Message<'a> = Message of Position * 'a
    with
        override x.ToString() = match x with | Message (pos, m) ->  sprintf "%O %O" pos m
