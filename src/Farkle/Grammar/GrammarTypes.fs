// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

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
            | InvalidTableCounts _ -> "The grammar does not contain the same count of tables as it should. If you see this error, please file an issue on GitHub."
            | IndexNotFound x -> sprintf "The index %d was not found in a list." x
            | NoErrorSymbol -> "The grammar has no symbol that signifies an error."
            | NoEOFSymbol -> "The grammar has no symbol that signifies the end of input."

/// Arbitrary metadata a grammar has.
/// A simple key-value collection.
type Properties = Properties of Map<string, string>

/// A set of characters. See `RangeSet` too.
type CharSet = RangeSet<char>

/// A symbol of a grammar
type Symbol =
    /// The symbol is a nonterminal.
    | Nonterminal of uint32 * string
    /// The symbol is a terminal.
    | Terminal of uint32 * string
    /// The symbol is noise (comments for example) and is discarded by the parser.
    | Noise of string
    /// The symbol signifies the end of input.
    | EndOfFile
    /// The symbol signifies the start of a group.
    | GroupStart of string
    /// The symbol signifies the end of a group.
    | GroupEnd of string
    /// The symbol signifies an error.
    | Error
    with
        
        /// The name of a symbol
        member x.Name =
            match x with
            | Nonterminal (_, x) -> x
            | Terminal (_, x) -> x
            | Noise x -> x
            | EndOfFile -> "EOF"
            | GroupStart x -> x
            | GroupEnd x -> x
            | Error -> "Error"
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
            match x with
            | Nonterminal _ -> sprintf "<%s>" x.Name
            | Terminal _ -> literalFormat x.Name
            | _ -> sprintf "(%s)" x.Name

/// Functions to work with `Symbol`s.
module Symbol =

    /// Creates a `Symbol`.
    let create name index =
        function
        | 0us -> Ok <| Nonterminal (index, name)
        | 1us -> Ok <| Terminal (index, name)
        | 2us -> Ok <| Noise name
        | 3us -> Ok EndOfFile
        | 4us -> Ok <| GroupStart name
        | 5us -> Ok <| GroupEnd name
        // 6 is deprecated
        | 7us -> Ok Error
        | x -> x |> InvalidSymbolType |> Result.Error

    /// Gets the name of a `Symbol`.
    let name (x: Symbol) = x.Name

/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

module internal AdvanceMode =

    let create =
        function
        | 0us -> Ok Token
        | 1us -> Ok Character
        | x -> x |> InvalidAdvanceMode |> Result.Error

/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

module internal EndingMode =

    let create =
        function
        | 0us -> Ok Open
        | 1us -> Ok Closed
        | x -> x |> InvalidEndingMode |> Result.Error

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
        |> Option.bind (flip Indexed.getfromList groups >> Trial.makeOption)

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
    override x.ToString() =
        let symbols = x.Handle |> List.map (string) |> String.concat " "
        sprintf "%O ::= %s" x.Head symbols

/// A DFA state. It defines the logic that produces `Tokens` out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
type DFA =
    {
        Transition: Map<uint32,Map<CharSet, uint32>>
        InitialState: uint32
        AcceptStates: Map<uint32, Symbol>
    }
    member x.Length = x.Transition.Count

type internal DFAState =
    /// This state accepts a symbol. If the state graph cannot be further walked, the included `Symbol` is returned.
    | DFAAccept of index: uint32 * (Symbol * (CharSet * Indexed<DFAState>) list)
    /// This state does not accept a symbol. If the state graph cannot be further walked and an accepting state has not been found, tokenizing fails.
    | DFAContinue of index: uint32 * edges: (CharSet * Indexed<DFAState>) list
    interface Indexable with
        member x.Index =
            match x with
            | DFAAccept (x, _) -> x
            | DFAContinue (x, _) -> x
    override x.ToString() = x :> Indexable |> Indexable.index |> string

module internal DFA =
    let create initial states =
        let extractStates = function | DFAAccept (index, (_, nextStates)) -> index, nextStates | DFAContinue (index, nextStates) -> index, nextStates
        let acceptStates = states |> Seq.choose (function | DFAAccept (index, (symbol, _)) -> Some (index, symbol) | DFAContinue _ -> None) |> Map.ofSeq
        let transition =
            states
            |> Seq.map (extractStates >> (fun (index, nextStates) -> index, nextStates |> Seq.map (fun (cs, Indexed(next)) -> cs, next) |> Map.ofSeq))
            |> Map.ofSeq
        {
            Transition = transition
            InitialState = initial
            AcceptStates = acceptStates
        }

/// An action to be taken by the parser.
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`..
    | Shift of uint32
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// This action is used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    | Goto of uint32
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and complete.
    | Accept

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
type internal LALRState =
    {
        /// The index of the state.
        Index: uint32
        /// The available `LALRAction`s of the state.
        /// Depending on the symbol, the next action to be taken is determined.
        Actions:Map<Symbol, LALRAction>
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() = string x.Index

type LALR =
    {
        InitialState: uint32
        States: Map<uint32, Map<Symbol, LALRAction>>
    }
    member x.Length = x.States.Count
    static member internal Create initial states =
        {
            InitialState = initial
            States = states |> Seq.map (fun {Index = i; Actions = actions} -> i, actions) |> Map.ofSeq
        }

module internal LALRAction =

    let create fProds index =
        function
        | 1us -> index |> Shift |> Ok
        | 2us -> index |> Indexed |> fProds |> Result.map Reduce
        | 3us -> index |> Goto |> Ok
        | 4us -> Accept |> Ok
        | x -> x |> InvalidLALRActionType |> Result.Error

/// A structure that specifies the initial DFA and LALR states.
type internal InitialStates =
    {
        /// The initial DFA state.
        /// Instead of the LALR state, it is reset when a `Token` is produced.
        DFA: DFAState
        /// The initial LALR state.
        /// Instead of the DFA state, it is set at the beginning of the parsing process and persists for its entirety.
        LALR: LALRState
    }

/// [omit]
module internal InitialStates =
    let dfa {DFA = x} = x
    let lalr {LALR = x} = x

/// A structure that describes a grammar - the logic under which a string is parsed.
/// Its constructor is private; use functions like these from the `EGT` module to create one.
type GOLDGrammar =
    private
        {
            _Properties: Properties
            _CharSets: CharSet RandomAccessList
            _Symbols: Symbol RandomAccessList
            _Groups: Group RandomAccessList
            _Productions: Production RandomAccessList
            _LALR: LALR
            _DFA: DFA
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
        /// The `Group`s of the grammar
        member x.Groups = x._Groups
        /// The `Production`s of the grammar.
        member x.Productions = x._Productions
        /// The grammar's LALR state table.
        member x.LALR = x._LALR
        /// The grammar's DFA state table.
        member x.DFA = x._DFA

/// [omit]
module internal GOLDGrammar =

    let counts (x: GOLDGrammar) =
        {
            SymbolTables = x.Symbols.Length |> uint16
            CharSetTables = x.CharSets.Length |> uint16
            ProductionTables = x.Productions.Length |> uint16
            DFATables = x.DFA.Length |> uint16
            LALRTables = x.LALR.Length |> uint16
            GroupTables = x.Groups.Length |> uint16
        }

    let properties {_Properties = x} = x
    let charSets {_CharSets = x} = x
    let symbols {_Symbols = x} = x
    let groups {_Groups = x} = x
    let productions {_Productions = x} = x
    let lalr {_LALR = x} = x
    let dfa {_DFA = x} = x

    let create properties symbols charSets prods dfas lalrs groups _counts = either {
        let g =
            {
                _Properties = properties
                _Symbols = symbols
                _CharSets = charSets
                _Productions = prods
                _DFA = dfas
                _LALR = lalrs
                _Groups = groups
            }
        let counts = counts g
        if counts = _counts then
            return g
        else
            return! (_counts, counts) |> InvalidTableCounts |> Result.Error}
