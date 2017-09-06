// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open FSharpx.Collections
open System
open System.Text

/// A token is an instance of a `Symbol`.
/// Tokens carry parsed data, as well as their position within the text file.
type Token =
    {
        /// The `Symbol` whose instance is this token.
        Symbol: Symbol
        /// The `Position` of the token in the input string.
        Position: Position
        /// The actual content of the token.
        Data: string
    }
    with
        /// [omit]
        static member Symbol_ :Lens<_, _> = (fun x -> x.Symbol), (fun v x -> {x with Symbol = v})
        /// [omit]
        static member Position_ :Lens<_, _> = (fun x -> x.Position), (fun v x -> {x with Position = v})
        /// [omit]
        static member Data_ :Lens<_, _> = (fun x -> x.Data), (fun v x -> {x with Data = v})
        /// A shortcut for creating a token.
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        /// Returns a new token which has a string appended to its data.
        static member AppendData data x = Optic.map Token.Data_ (fun x -> x + data) x
        override x.ToString() = x.Data

module internal Token =

    let dummy sym = {Symbol = sym; Position = Position.initial; Data = ""}

/// A reduction  will contain the tokens which correspond to the symbols of a `Production`.
/// Since a reduction contains the terminals of a rule as well as the nonterminals (reductions made earlier),
/// the parser engine creates a "parse tree" which contains a break down of the source text along the grammar's rules
type Reduction =
    {
        /// The `Token`s of the reduction.
        /// Some of them also have another reduction assosicated.
        Tokens: (Token * Reduction option) list
        /// The `Production` which the reduction's tokens correspond to.
        Parent: Production
    }
    override x.ToString() = x.Tokens |> List.map (fst >> Optic.get Token.Data_) |> String.concat ""

/// Functions to work with `Reduction`s.
module Reduction =

    /// Visualizes a `Reduction` in the form of a textual "parse tree".
    [<CompiledName("DrawReductionTree")>]
    let drawReductionTree x =
        let sb = StringBuilder()
        let append (x: string) =
            sb.Append x |> ignore
            sb.AppendLine() |> ignore
        let kIndentText = "|  "
        let rec impl indent {Parent = parent; Tokens = tokens} =
            let indentText = kIndentText |> Seq.replicate indent |> Seq.concat |> Array.ofSeq |> String
            sprintf "%s+--%O" indentText parent |> append |> ignore
            tokens
            |> List.iter (
                function
                | (_, Some x) -> impl (indent + 1) x
                | (x, None) -> sprintf "%s%s+--%O" indentText kIndentText x.Data |> append |> ignore)
        impl 0 x
        sb.ToString()

/// An internal error. These errors are known errors a program might experience.
/// They could occur by manipulating the parser internal state, which is _impossible_ from the public API.
/// Another possible way of manifestation is creating deliberately faulty `Grammar`s.
type ParseInternalError =
    /// After a reduction, the next LALR action should be a `Goto` one.
    /// But it's not.
    | GotoNotFoundAfterReduction of Production * Indexed<LALRState>
    /// The item at a given index of a list was not found.
    | IndexNotFound of uint32
    /// The LALR stack is empty; it should never be.
    | LALRStackEmpty

type internal LALRResult =
    | Accept of Reduction
    | Shift of Indexed<LALRState>
    | ReduceNormal of Reduction
    | ReduceEliminated
    | SyntaxError of expected: Symbol list * actual: Symbol
    | InternalError of ParseInternalError

/// A message describing what a `Parser` did, or what error it encountered.
/// Some of these will only appear while using the `GOLDParser` API.
type ParseMessage =
    /// A token was read.
    | TokenRead of Token
    /// A rule was reduced.
    | Reduction of Reduction
    /// The parser shifted to a different LALR state.
    | Shift of Indexed<LALRState>
    /// The parser finished parsing and returned a reduction.
    | Accept of Reduction
    /// Something is wrong with the reading of the EGT file.
    | EGTReadError of EGTReadError
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
    /// Whether the message signifies an error or not.
    member x.IsError =
        match x with
        | TokenRead _ | Reduction _ | Accept _ | Shift _ -> false
        | EGTReadError _ | InputFileNotExist _ | LexicalError _ | SyntaxError _ | GroupError | InternalError _ -> true
    override x.ToString() =
        match x with
        | EGTReadError x -> sprintf "Error while reading the EGT file: %O" x
        | InputFileNotExist x -> sprintf "File \"%s\" does not exist" x
        | TokenRead x -> sprintf "Token read: \"%O\" (%s)" x x.Symbol.Name
        | Reduction x -> sprintf "Rule reduced: %O (%O)" x.Parent x
        | Shift (Indexed x) -> sprintf "The parser shifted to state %d" x
        | Accept x -> sprintf "Reduction accepted: %O (%O)" x.Parent x
        | LexicalError x -> sprintf "Cannot recognize token: %c" x
        | SyntaxError (expected, actual) ->
            let expected = expected |> List.map (sprintf "\"%O\"") |> String.concat ", "
            sprintf "Found %O, while expecting one of the following tokens: %O" actual expected
        | GroupError -> "Unexpected end of input"
        | InternalError x -> sprintf "Internal error: %O. This is most probably a bug. If you see this error, please file an issue on GitHub." x

type internal ParserState =
    {
        Grammar: Grammar
        InputStream: char list
        CurrentLALRState: LALRState
        InputStack: Token list
        LALRStack: (Token * (LALRState * Reduction option)) list
        TrimReductions: bool
        CurrentPosition: Position
        GroupStack: Token list
    }
    with
        static member grammar x = x.Grammar
        static member InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
        static member CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
        static member InputStack_ :Lens<_, _> = (fun x -> x.InputStack), (fun v x -> {x with InputStack = v})
        static member LALRStack_ :Lens<_, _> = (fun x -> x.LALRStack), (fun v x -> {x with LALRStack = v})
        static member trimReductions x = x.TrimReductions
        static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
        static member GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})

module internal ParserState =

    /// Creates a parser state.
    let create trimReductions grammar input =
        {
            Grammar = grammar
            InputStream = input
            CurrentLALRState = grammar.InitialStates.LALR
            InputStack = []
            LALRStack = [Token.dummy grammar.ErrorSymbol, (grammar.InitialStates.LALR, None)]
            TrimReductions = trimReductions
            CurrentPosition = Position.initial
            GroupStack = []
        }

/// A type signifying the state of a parser.
/// The parsing process can be continued by evaluating the lazy `Parser` values.
/// This type is the lowest-level public API.
/// It is modeled after the [Designing with Capabilities](https://fsharpforfunandprofit.com/cap/) presentation.
type Parser =
    /// The parser has completed one step of the parsing process.
    /// The log message of it is returned as well as a thunk of the next parser.
    | Continuing of (Position * ParseMessage) * Parser Lazy
    /// The parser has failed.
    /// No lazy parser is returned, so the parsing process cannot continue.
    | Failed of Position * ParseMessage
    /// The parser has finished parsing.
    /// No lazy parser is returned, as the parsing process is complete.
    | Finished of Position * Reduction
