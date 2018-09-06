// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open System
open System.Text

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

type LALRResult =
    | Accept of AST
    | Shift of uint32
    | ReduceNormal of AST
    | SyntaxError of expected: Symbol list * actual: Symbol
    | InternalError of ParseInternalError

/// An action of the parser.
type ParseMessageType =
    /// A token was read.
    | TokenRead of Token
    /// A rule was reduced.
    | Reduction of AST
    /// The parser shifted to a different LALR state.
    | Shift of uint32
    /// The parser finished parsing and returned an AST.
    | Accept of AST
    override x.ToString() =
        match x with
        | TokenRead x -> sprintf "Token read: \"%O\" (%s)" x x.Symbol.Name
        | Reduction x -> sprintf "Rule reduced: %O" x
        | Shift x -> sprintf "The parser shifted to state %d" x
        | Accept x -> sprintf "Abstract Syntax Tree accepted: %O" x

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
type ParseError = ParseError of Position * ParseErrorType
    with
        override x.ToString() = match x with | ParseError (pos, mt) ->  sprintf "%O %O" pos mt

/// The feedback from the tokenizer after a token is read.
/// Because the tokenizer is completely isolated, it needs to provide
/// some more information to the rest of the code than just the token.
/// That's the reason of this type.
type TokenizerFeedback =
    {
        /// The `Token` the tokenizer returns.
        /// Its `SymbolType` gives more information, like whether
        /// the tokenizer encountered an error, or reached the end.
        NewToken: Token
        /// The position the tokenizer is.
        /// It is needed for the parser to generate accurate error reports.
        CurrentPosition: Position
        /// Whether the tokenizer is inside a lexical group.
        /// It is needed for the parser to determine whether a `GroupError` occured.
        IsGroupStackEmpty: bool
    }

/// A tokenizer. What is it actually? An infinite stream of tokens (and some other information).
/// It is infinite because the architecture currently requires it.
/// But after one point, the tokenizer returns just EOFs forever.
type Tokenizer = EndlessProcess<TokenizerFeedback>

/// A LALR parser. It takes a `Token`, and gives an `LALRResult`.
/// It is a stateful operation; the type of this state
/// is abstracted from the rest of the parser.
type LALRParser = LALRParser of (Token -> (LALRResult * LALRParser))

type internal ParserState =
    {
        TheTokenizer: Tokenizer
        TheLALRParser: LALRParser
        InputStack: Token list
        IsGroupStackEmpty: bool
        CurrentPosition: Position
    }
    static member TheTokenizer_ :Lens<_, _> = (fun x -> x.TheTokenizer), (fun v x -> {x with TheTokenizer = v})
    static member TheLALRParser_ :Lens<_, _> = (fun x -> x.TheLALRParser), (fun v x -> {x with TheLALRParser = v})
    static member InputStack_ :Lens<_, _> = (fun x -> x.InputStack), (fun v x -> {x with InputStack = v})
    static member IsGroupStackEmpty_ :Lens<_, _> = (fun x -> x.IsGroupStackEmpty), (fun v x -> {x with IsGroupStackEmpty = v})
    static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})

module internal ParserState =

    /// Creates a parser state.
    let create tokenizer lalrParser =
        {
            TheTokenizer = tokenizer
            TheLALRParser = lalrParser
            InputStack = []
            IsGroupStackEmpty = true
            CurrentPosition = Position.initial
        }

/// A type signifying the state of a parser.
/// The parsing process can be continued by evaluating the lazy `Parser` values.
/// This type is the lowest-level public API.
/// It is modeled after the [Designing with Capabilities](https://fsharpforfunandprofit.com/cap/) presentation.
type Parser =
    /// The parser has completed one step of the parsing process.
    /// The log message of it is returned as well as a thunk of the next parser.
    | Continuing of ParseMessage * Parser Lazy
    /// The parser has failed.
    /// No lazy parser is returned, so the parsing process cannot continue.
    | Failed of ParseMessage
    /// The parser has finished parsing.
    /// No lazy parser is returned, as the parsing process is complete.
    | Finished of ParseMessage * AST
