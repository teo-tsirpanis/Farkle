// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Aether
open Chessie.ErrorHandling
open FSharpx.Collections
open Farkle
open System

/// A record that stores how many of each tables exist in an EGT file.
/// It's needed only for verifying that the grammar was successfuly read.
type TableCounts =
    {
        /// How many symbols exist.
        SymbolTables: uint16
        /// How many character sets exist.
        CharSetTables: uint16
        /// How many productions exist.
        ProductionTables: uint16
        /// How many DFA states exist.
        DFATables: uint16
        /// How many LALR states exist.
        LALRTables: uint16
        /// How many groups exist.
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
    | InvalidEntryCode of byte
    /// An entry of `expected` type was requested, but something else was returned instead.
    | InvalidEntryType of expected: string
    /// The string you asked for is not terminated
    | UnterminatedString
    /// takeString has a bug. The developer _should_ be contacted
    /// in case this type of error is encountered
    | TakeStringBug
    /// Records should start with `M`, but this one started with something else.
    | InvalidRecordTag of byte
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
    | IndexNotFound of uint32
    /// The grammar has no symbol of type `Error`.
    | NoErrorSymbol
    /// The grammar has no symbol of type `EndOfFile`.
    | NoEOFSymbol
    with
        override x.ToString() =
            match x with
            | ListError x -> sprintf "List error: %O" x
            | InvalidBoolValue x -> sprintf "Invalid boolean value (neither 0 nor 1): %d." x
            | InvalidEntryCode x -> x |> char |> sprintf "Invalid entry code: '%c'."
            | InvalidEntryType x -> sprintf "Unexpected entry type. Expected a %s." x
            | UnterminatedString -> "String terminator was not found."
            | TakeStringBug -> "The function takeString exhibited a very unlikely bug. If you see this error, please file an issue on GitHub."
            | InvalidRecordTag x -> x |> char |> sprintf "The record tag '%c' is not 'M', as it should have been."
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
            | NoErrorSymbol -> "The grammar has no symbol that signifies an error."
            | NoEOFSymbol -> "The grammar has no symbol that signifies the end of input."

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
module internal SymbolType =

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
        /// The symbol's index as it appears in the grammar file.
        Index: uint32
        /// The symbol's type.
        SymbolType: SymbolType
    }
    with
        interface Indexable with
            member x.Index = x.Index
        override x.ToString() =
            let literalFormat x =
                let forceDelimiter =
                    x = ""
                    || x.[0] |> Char.IsLetter
                    || x |> String.forall (fun x -> Char.IsLetter x || x = '.' || x = '-' || x = '_')
                if forceDelimiter then
                    sprintf "'%s'" x
                else
                    x
            match x.SymbolType with
            | Nonterminal -> sprintf "<%s>" x.Name
            | Terminal -> literalFormat x.Name
            | _ -> sprintf "(%s)" x.Name

/// Functions to work with `Symbol`s.
module Symbol =

    /// Gets the name of a `Symbol`.
    let name {Name = x} = x

    /// Gets the type of a `Symbol`.
    let symbolType {SymbolType = x} = x

/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

module internal AdvanceMode =

    let create =
        function
        | 0us -> ok Token
        | 1us -> ok Character
        | x -> x |> InvalidAdvanceMode |> fail

/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

module internal EndingMode =

    let create =
        function
        | 0us -> ok Open
        | 1us -> ok Closed
        | x -> x |> InvalidEndingMode |> fail

/// A structure that describes a lexical group.
/// In GOLD, lexical groups are used for situations where a number of recognized tokens should be organized into a single "group".
/// This mechanism is most commonly used to handle line and block comments.
/// However, it is not limited to "noise", but can be used for any content.
type Group =
    {
        /// The name of the group.
        Name: string
        /// The index of the group as it appears on the grammar file.
        Index: uint32
        /// The symbol that represents the group's content.
        ContainerSymbol: Symbol
        /// The symbol that represents the group's start.
        StartSymbol: Symbol
        /// The symbol that represents the group's end.
        EndSymbol: Symbol
        /// The way the group advances. Also see [AdvanceMode].
        AdvanceMode: AdvanceMode
        /// The way the group ends. Also see [EndingMode].
        EndingMode: EndingMode
        /// A set of indexes whose corresponding groups can be nested inside this group.
        Nesting: Set<Indexed<Group>>
    }
    interface Indexable with
        member x.Index = x.Index

/// Functions to work with `Group`s.
module Group =

    /// [omit]
    let name {Group.Name = x} = x
    /// [omit]
    let containerSymbol {ContainerSymbol = x} = x
    /// [omit]
    let startSymbol {StartSymbol = x} = x
    /// [omit]
    let endSymbol {EndSymbol = x} = x
    /// [omit]
    let advanceMode {AdvanceMode = x} = x
    /// [omit]
    let endingMode {EndingMode = x} = x
    /// [omit]
    let nesting {Nesting = x} = x

    /// Gets the index of the group in a list tha has the specified symbol either its start, or end, or container symbol.
    /// Such index might not exist; in this case, `None` is returned.
    let getSymbolGroupIndexed groups x: Indexed<Group> option =
        groups
        |> Seq.tryFindIndex (fun {ContainerSymbol = x1; StartSymbol = x2; EndSymbol = x3} -> x = x1 || x = x2 || x = x3)
        |> Option.map (uint32 >> Indexed)

    /// Like `getSymbolGroupIndexed`, but returns a `Group`, instead of an index.
    /// Such group might not exist; in this case, `None` is returned.
    let getSymbolGroup groups x =
        (groups, x)
        ||> getSymbolGroupIndexed
        |> Option.bind (Indexed.getfromList groups >> Trial.makeOption)

/// The basic building block of a grammar's syntax.
/// It consists of a single nonterminal called the "head".
/// The head is defined to consist of multiple symbols making up the production's "handle".
type Production =
    {
        /// The index of the production as it appears on the grammar file.
        Index: uint32
        /// The head of the production.
        Head: Symbol
        /// The handle of the production.
        Handle: Symbol list
    }
    interface Indexable with
        member x.Index = x.Index
    /// Returns true if the production's handle consists of only one `NonTerminal`, and false otherwise.
    member x.HasOneNonTerminal =
        match x.Handle with
        | [x] -> x.SymbolType = Nonterminal
        | _ -> false
    override x.ToString() =
        let symbols = x.Handle |> List.map (string) |> String.concat " "
        sprintf "%O ::= %s" x.Head symbols

/// A DFA state. Many of them define the logic that produces `Tokens` out of strings.
type DFAState =
    {
        /// The index of the state.
        Index: uint32
        /// Each DFA state can accept one of the grammar's terminal symbols. If the state accepts a terminal symbol, the value will be set to Some and will contain the symbol.
        AcceptSymbol: Symbol option
        /// Each edge contains a series of characters that are used to determine whether the Deterministic Finite Automata will follow it. It is linked to state in the DFA Table.
        Edges: (CharSet * Indexed<DFAState>) list
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() = string x.Index

/// [omit]
module DFAState =
    let acceptSymbol {AcceptSymbol = x} = x
    let edges {Edges = x} = x

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
type LALRState =
    {
        /// The index of the state.
        Index: uint32
        /// The available `LALRAction`s of the state.
        /// Depending on the symbol, the next action to be taken is determined.
        States:Map<Symbol, LALRAction>
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() = string x.Index

/// An action to be taken by the parser.
and LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`..
    | Shift of Indexed<LALRState>
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// This action is used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    | Goto of Indexed<LALRState>
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and complete.
    | Accept

module internal LALRAction =

    let create (fProds: Indexed<Production> -> Result<Production, EGTReadError>) index =
        function
        | 1us -> index |> Indexed |> Shift |> ok
        | 2us -> index |> Indexed |> fProds |> lift Reduce
        | 3us -> index |> Indexed |> Goto |> ok
        | 4us -> Accept |> ok
        | x -> x |> InvalidLALRActionType |> fail

/// A structure that specifies the initial DFA and LALR states.
type InitialStates =
    {
        /// The initial DFA state.
        /// Instead of the LALR state, it is reset when a `Token` is produced.
        DFA: DFAState
        /// The initial LALR state.
        /// Instead of the DFA state, it is set at the beginning of the parsing process and persists for its entirety.
        LALR: LALRState
    }

/// [omit]
module InitialStates =
    let dfa {DFA = x} = x
    let lalr {LALR = x} = x

/// A structure that describes a grammar - the logic under which a string is parsed.
/// Its constructor is private; use functions like these from the `EGT` module to create one.
type Grammar =
    private
        {
            _Properties: Properties
            _CharSets: CharSet RandomAccessList
            _Symbols: Symbol RandomAccessList
            _ErrorSymbol: Symbol
            _EOFSymbol: Symbol
            _Groups: Group RandomAccessList
            _Productions: Production RandomAccessList
            _InitialStates: InitialStates
            _LALRStates: LALRState RandomAccessList
            _DFAStates: DFAState RandomAccessList
        }
    with
        /// The `Properties` of the grammar.
        /// They are just metadata; they are not used by Farkle.
        member x.Properties = x._Properties
        /// The `CharSet`s of the grammar.
        /// This might be removed in the future.
        member x.CharSets = x._CharSets
        /// The `Symbol`s of the grammar.
        member x.Symbols = x._Symbols
        /// The first symbol of type `Error`.
        member x.ErrorSymbol = x._ErrorSymbol
        /// THe first symbol of type `EndOfInput`.
        member x.EOFSymbol = x._EOFSymbol
        /// The `Group`s of the grammar
        member x.Groups = x._Groups
        /// The `Production`s of the grammar.
        member x.Productions = x._Productions
        /// The initial LALR and DFA states of a grammar.
        member x.InitialStates = x._InitialStates
        /// The grammar's LALR state table.
        member x.LALRStates = x._LALRStates
        /// The grammar's DFA state table.
        member x.DFAStates = x._DFAStates

/// [omit]
module Grammar =

    let internal counts (x: Grammar) =
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

    let create properties symbols charSets prods initialStates dfas lalrs groups _counts = trial {
        let firstOfType err x = symbols |> Seq.tryFind (Symbol.symbolType >> ((=) x)) |> failIfNone err
        let! errorSymbol = firstOfType NoErrorSymbol Error
        let! eofSymbol = firstOfType NoEOFSymbol EndOfFile
        let g =
            {
                _Properties = properties
                _Symbols = symbols
                _ErrorSymbol = errorSymbol
                _EOFSymbol = eofSymbol
                _CharSets = charSets
                _Productions = prods
                _InitialStates = initialStates
                _DFAStates = dfas
                _LALRStates = lalrs
                _Groups = groups
            }
        let counts = counts g
        if counts = _counts then
            return g
        else
            return! (_counts, counts) |> InvalidTableCounts |> fail}
