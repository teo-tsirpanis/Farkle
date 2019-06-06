// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar

/// An internal error. These errors are known errors a parser might experience.
/// An encounter of it is most certainly a library bug (or deliberately corrupted grammars).
/// The user is encouraged to report it to GitHub.
/// The `ToString()` method is not overriden here, because users don't need to know that many details about them.
type InternalError =
    /// After a reduction, a corresponding GOTO action should have been found but wasn't.
    | GotoNotFoundAfterReduction of Production * LALRState
    /// The LALR stack is empty; it should never be.
    | LALRStackEmpty
    /// The LALR table says to shift when input ends.
    | ShiftOnEOF

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
[<RequireQualifiedAccess>]
type ParseMessage =
    /// Input ended.
    | EndOfInput
    /// A token was read.
    | TokenRead of Token
    /// A rule was reduced.
    | Reduction of Production
    /// The parser shifted to a different LALR state.
    | Shift of uint32
    override x.ToString() =
        match x with
        | EndOfInput -> "Input ended"
        | TokenRead x -> sprintf "%O Token read: %O (%s)" x.Position x x.Symbol.Name
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
    /// The post-processor had a problem transforming a terminal.
    | TransformError of Terminal * exn
    /// The post-processor had a problem fusing the tokems of a production.
    | FuseError of Production * exn
    /// The post-processor did not find an appropriate fuser for a production.
    | FuserNotFound of Production
    /// Internal error. This is a bug.
    | InternalError of InternalError
    override x.ToString() =
        match x with
        | LexicalError x -> sprintf "Cannot recognize character `%c`" x
        | SyntaxError (expected, actual) ->
            let expected = expected |> Seq.map string |> String.concat ", "
            sprintf "Found `%O`, while expecting one of the following tokens: %s" actual expected
        | CannotNestGroups(g1, g2) -> sprintf "Group `%O` cannot be nested inside `%O`" g1 g2
        | UnexpectedGroupEnd ge -> sprintf "`%O` was encountered outside of any group." ge
        | UnexpectedEndOfInput g -> sprintf "Unexpected end of input while being inside a %s." g.Name
        | TransformError (term, ex) -> sprintf "Exception in user code while post-processing terminal `%O`.\nException:\n%O" term ex
        | FuseError (prod, ex) -> sprintf "Exception in user code while post-processing production `%O`.\nException:\n%O" prod ex
        | FuserNotFound prod -> sprintf "Configuration error: No fuser specified for production `%O`.\nYou have to write one, and recompile your program." prod
        | InternalError x -> sprintf "Internal error. This is most probably a bug in the parser. If you see this error, please file an issue on GitHub.\nDetails:\n%A" x

/// A log message that contains a position it was encountered.
type Message<'a> = Message of Position * 'a
    with
        override x.ToString() = let (Message(pos, m)) = x in sprintf "%O %O" pos m
