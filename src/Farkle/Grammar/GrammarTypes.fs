// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Aether
open Chessie.ErrorHandling
open Farkle

/// A record that stores how many of each structures exist in an EGT file.
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

type Properties = Properties of Map<string, string>

type CharSet = RangeSet<char>

type SymbolType =
    | Nonterminal
    | Terminal
    | Noise
    | EndOfFile
    | GroupStart
    | GroupEnd
    // 6 is deprecated
    | Error

module SymbolType =

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

type Symbol =
    {
        Name: string
        Kind: SymbolType
    }
    with
        static member Error = {Name = "Error"; Kind = Error}
        static member EOF = {Name = "EOF"; Kind = EndOfFile}

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

type Production =
    {
        Nonterminal: Symbol
        Symbols: Symbol list
    }

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
