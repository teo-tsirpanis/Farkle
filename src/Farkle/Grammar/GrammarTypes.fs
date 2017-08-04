// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Aether
open Chessie.ErrorHandling
open Farkle
open System

/// A record that stores how many of each tables exist in an EGT file.
/// It's needed only for verifying that the grammar was successfuly read.
type TableCounts =
    {
        SymbolTables: uint16
        CharSetTables: uint16
        ProductionTables: uint16
        DFATables: uint16
        LALRTables: uint16
        GroupTables: uint16
    }

/// What can go wrong with reading an EGT file.
type EGTReadError =
    /// A [sequence error](Farkle.SeqError) did happen.
    | ListError of ListError
    /// Boolean values should only be `0` or `1`.
    /// If they are not, thet it's undefined by the documentation.
    /// But we will call it an error.
    | InvalidBoolValue of byte
    /// An invalid entry code was encountered.
    /// Valid entry codes are these letters: `EbBIS`.
    | InvalidEntryCode of char
    /// An entry of `expected` type was requested, but something else was returned instead.
    | InvalidEntryType of expected: string
    /// The string you asked for is not terminated
    | UnterminatedString
    /// takeString has a bug. The developer _should_ be contacted
    /// in case this type of error is encountered
    | TakeStringBug
    /// Records should start with `M`, but this one started with something else.
    | InvalidRecordTag of char
    /// The file's header is invalid.
    | UnknownFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile
    /// The file you specified does not exist.
    | FileNotExist of string
    /// The type of a symbol is invalid.
    | InvalidSymbolType of uint16
    /// The advancing mode of a group is invalid.
    | InvalidAdvanceMode of uint16
    /// The ending mode of a group is invalid.
    | InvalidEndingMode of uint16
    /// The type of an LALR action is invalid.
    | InvalidLALRActionType of uint16
    /// Some records of the EGT file were not read.
    /// More or less were expected.
    | InvalidTableCounts of expected: TableCounts * actual: TableCounts
    /// The item at the given index of a list was not found.
    | IndexNotFound of uint16
    with
        override x.ToString() =
            match x with
            | ListError x -> sprintf "List error: %A" x
            | InvalidBoolValue x -> sprintf "Invalid boolean value (neither 0 nor 1): %d." x
            | InvalidEntryCode x -> sprintf "Invalid entry code: '%c'." x
            | InvalidEntryType x -> sprintf "Unexpected entry type. Expected a %s." x
            | UnterminatedString -> "String terminator was not found."
            | TakeStringBug -> "The function takeString exhibited a very unlikely bug. If you see this error, please file an issue on GitHub."
            | InvalidRecordTag x -> sprintf "The record tag '%c' is not 'M', as it should have been." x
            | UnknownFile -> "The given file is not recognized."
            | ReadACGTFile ->
                "The given file is a CGT file, not an EGT one."
                + " You should update to the latest version of GOLD Parser Builder (at least over Version 5.0.0)"
                + " and save the tables as \"Enhanced Grammar tables (Version 5.0)\"."
            | FileNotExist x -> sprintf "The given file (%s) does not exist." x
            | InvalidSymbolType x -> sprintf "Invalid symbol type (should be 0, 1, 2, 3, 4, 5 or 7): %d." x
            | InvalidAdvanceMode x -> sprintf "Invalid advance code (should be either 0 or 1): %d." x
            | InvalidEndingMode x -> sprintf "Invalid ending mode value (should be either 0 or 1): %d." x
            | InvalidLALRActionType x -> sprintf "Invalid LALR action index (should be 1, 2, 3 or 4): %d." x
            | InvalidTableCounts (expected, actual) -> "The grammar does not contain the same count of tables as it should. If you see this error, please file an issue on GitHub."
            | IndexNotFound x -> sprintf "The index %d was not found in a list." x

/// Arbitrary metadata a grammar has.
/// A simple key-value collection.
type Properties = Properties of Map<string, string>

/// A set of characters. See `RangeSet` too.
type CharSet = RangeSet<char>

/// The type of a symbol
type SymbolType =
    /// The symbol is a nonterminal.
    | Nonterminal
    /// The symbol is a terminal.
    | Terminal
    /// The symbol is noise (comments for example) and is discarded by the parser.
    | Noise
    /// The symbol signifies the end of input.
    | EndOfFile
    /// The symbol signifies the start of a group.
    | GroupStart
    /// The symbol signifies the end of a group.
    | GroupEnd
    /// The symbol signifies an error.
    | Error

/// Functions to work with the `SymbolType` type.
module SymbolType =

    /// Creates a `SymbolType`.
    let ofUInt16 =
        function
        | 0us -> ok Nonterminal
        | 1us -> ok Terminal
        | 2us -> ok Noise
        | 3us -> ok EndOfFile
        | 4us -> ok GroupStart
        | 5us -> ok GroupEnd
        // 6 is deprecated
        | 7us -> ok Error
        | x -> x |> InvalidSymbolType |> fail

/// A symbol of a grammar.
type Symbol =
    {
        /// The symbol's name.
        Name: string
        /// The symbol's type.
        SymbolType: SymbolType
    }
    with
        /// A special symbol that signifies an error.
        /// It's the same in all grammars, so it's not worth taking it from the symbol table.
        static member Error = {Name = "Error"; SymbolType = Error}
        /// A special symbol that signifies the end of input.
        /// It's the same in all grammars, so it's not worth taking it from the symbol table.
        static member EOF = {Name = "EOF"; SymbolType = EndOfFile}
        member x.ToString delimitTerminals =
            let literalFormat forceDelimiter x =
                let forceDelimiter =
                    forceDelimiter
                    || x = ""
                    || x.[0] |> Char.IsLetter
                    || x |> String.exists (fun x -> Char.IsLetter x || x = '.' || x = '-' || x = '_') |> not
                if forceDelimiter then
                    sprintf "'%s'" x
                else
                    x
            match x.SymbolType with
            | Nonterminal -> sprintf "<%s>" x.Name
            | Terminal -> literalFormat delimitTerminals x.Name
            | _ -> sprintf "(%s)" x.Name
        
        override x.ToString() = x.ToString false

module Symbol =

    let name {Name = x} = x

    let symbolType {SymbolType = x} = x

type AdvanceMode =
    | Token
    | Character

module AdvanceMode =

    let create =
        function
        | 0us -> ok Token
        | 1us -> ok Character
        | x -> x |> InvalidAdvanceMode |> fail

type EndingMode =
    | Open
    | Closed

module EndingMode =

    let create =
        function
        | 0us -> ok Open
        | 1us -> ok Closed
        | x -> x |> InvalidEndingMode |> fail

type Group =
    {
        Name: string
        ContainerSymbol: Symbol
        StartSymbol: Symbol
        EndSymbol: Symbol
        AdvanceMode: AdvanceMode
        EndingMode: EndingMode
        Nesting: Set<Indexed<Group>>
    }

module Group =

    let name {Group.Name = x} = x
    let containerSymbol {ContainerSymbol = x} = x
    let startSymbol {StartSymbol = x} = x
    let endSymbol {EndSymbol = x} = x
    let advanceMode {AdvanceMode = x} = x
    let endingMode {EndingMode = x} = x
    let nesting {Nesting = x} = x

    let getSymbolGroupIndexed groups x: Indexed<Group> option =
        groups
        |> List.tryFindIndex (fun {ContainerSymbol = x1; StartSymbol = x2; EndSymbol = x3} -> x = x1 || x = x2 || x = x3)
        |> Option.map (uint16 >> Indexed)

    let getSymbolGroup groups x =
        (groups, x)
        ||> getSymbolGroupIndexed
        |> Option.bind (Indexed.getfromList groups >> Trial.makeOption)

type Production =
    {
        Nonterminal: Symbol
        Symbols: Symbol list
    }

module Production =

    let hasOneNonTerminal =
        function
        | {Symbols = [{SymbolType = x}]} -> x = Nonterminal
        | _ -> false

type DFAState =
    {
        AcceptSymbol: Symbol option
        Edges: Set<CharSet * Indexed<DFAState>>
    }

module DFAState =
    let acceptSymbol {AcceptSymbol = x} = x
    let edges {Edges = x} = x

type LALRState = LALRState of Map<Symbol, LALRAction>

and LALRAction =
    | Shift of Indexed<LALRState>
    | Reduce of Production
    | Goto of Indexed<LALRState>
    | Accept

module LALRAction =

    let create (fProds: Indexed<Production> -> Result<Production, EGTReadError>) index =
        function
        | 1us -> index |> Indexed |> Shift |> ok
        | 2us -> index |> Indexed |> fProds |> lift Reduce
        | 3us -> index |> Indexed |> Goto |> ok
        | 4us -> Accept |> ok
        | x -> x |> InvalidLALRActionType |> fail

type InitialStates =
    {
        DFA: DFAState
        LALR: LALRState
    }

module InitialStates =
    let dfa {DFA = x} = x
    let lalr {LALR = x} = x

type Grammar =
    private
        {
            _Properties: Properties
            _CharSets: CharSet list
            _Symbols: Symbol list
            _Groups: Group list
            _Productions: Production list
            _InitialStates: InitialStates
            _LALRStates: LALRState list
            _DFAStates: DFAState list
        }
    with
        member x.Properties = x._Properties
        member x.CharSets = x._CharSets
        member x.Symbols = x._Symbols
        member x.Groups = x._Groups
        member x.Productions = x._Productions
        member x.InitialStates = x._InitialStates
        member x.LALRStates = x._LALRStates
        member x.DFAStates = x._DFAStates

module Grammar =

    let counts (x: Grammar) =
        {
            SymbolTables = x.Symbols.Length |> uint16
            CharSetTables = x.CharSets.Length |> uint16
            ProductionTables = x.Productions.Length |> uint16
            DFATables = x.DFAStates.Length |> uint16
            LALRTables = x.LALRStates.Length |> uint16
            GroupTables = x.Groups.Length |> uint16
        }

    let properties {_Properties = x} = x
    let charSets {_CharSets = x} = x
    let symbols {_Symbols = x} = x
    let groups {_Groups = x} = x
    let productions {_Productions = x} = x
    let initialStates {_InitialStates = x} = x
    let lalr {_LALRStates = x} = x
    let dfa {_DFAStates = x} = x

    let create properties symbols charSets prods initialStates dfas lalrs groups _counts =
        let g =
            {
                _Properties = properties
                _Symbols = symbols
                _CharSets = charSets
                _Productions = prods
                _InitialStates = initialStates
                _DFAStates = dfas
                _LALRStates = lalrs
                _Groups = groups
            }
        let counts = counts g
        if counts = _counts then
            ok g
        else
            (_counts, counts) |> InvalidTableCounts |> fail
