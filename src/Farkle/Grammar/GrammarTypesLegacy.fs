// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// The old domain model lies here.
module internal Farkle.Grammar.Legacy

open Farkle
open Farkle.Collections
open System
open System.Collections.Generic
open System.Collections.Immutable

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

/// Arbitrary metadata a grammar has.
/// A simple key-value collection.
type Properties = Map<string, string>

/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

/// A structure that describes a lexical group.
/// In GOLD, lexical groups are used for situations where a number of recognized tokens should be organized into a single "group".
/// This mechanism is most commonly used to handle line and block comments.
/// However, it is not limited to "noise", but can be used for any content.
[<Struct>]
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
        /// The way the group advances.
        AdvanceMode: AdvanceMode
        /// The way the group ends.
        EndingMode: EndingMode
        /// A set of indexes whose corresponding groups can be nested inside this group.
        Nesting: Set<Indexed<Group>>
    }


/// A symbol of a grammar.
and Symbol =
    /// The symbol is a nonterminal.
    | Nonterminal of uint32 * string
    /// The symbol is a terminal.
    | Terminal of uint32 * string
    /// The symbol is noise (comments for example) and is discarded by the parser.
    | Noise of string
    /// The symbol signifies the end of input.
    | EndOfFile
    /// The symbol signifies the start of a group.
    | GroupStart of Indexed<Group> * (uint32 * string)
    /// The symbol signifies the end of a group.
    | GroupEnd of uint32 * string
    /// This symbol type is deprecated and should not be used anymore.
    // It used to represent an unrecognized symbol
    | SymbolTypeUnused
    with

        /// The name of a symbol
        member x.Name =
            match x with
            | Nonterminal (_, x) -> x
            | Terminal (_, x) -> x
            | Noise x -> x
            | EndOfFile -> "EOF"
            | GroupStart (_, (_, x)) -> x
            | GroupEnd (_, x) -> x
            | SymbolTypeUnused -> "Unrecognized"
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
module internal Symbol =

    /// Returns the index of a terminal symbol, or nothing.
    let tryGetTerminalIndex = function Terminal (index, _) -> Some index | _ -> None

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
        Handle: Symbol ImmutableArray
    }
    override x.ToString() =
        let symbols = x.Handle |> Seq.map string |> String.concat " "
        sprintf "%O ::= %s" x.Head symbols

/// A DFA state. It defines the logic that produces `Tokens` out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
type DFAState =
    /// This state accepts a symbol. If the state graph cannot be further walked, the included `Symbol` is returned.
    | DFAAccept of index: uint32 * (Symbol * RangeMap<char,Indexed<DFAState>>)
    /// This state does not accept a symbol. If the state graph cannot be further walked and an accepting state has not been found, tokenizing fails.
    | DFAContinue of index: uint32 * edges: RangeMap<char,Indexed<DFAState>>
    member x.Index =
        match x with
        | DFAAccept (x, _) | DFAContinue (x, _) -> x
    /// Returns the edges of the DFA state.
    member x.Edges =
        match x with
        | DFAAccept (_, (_, e)) | DFAContinue (_, e) -> e
    override x.ToString() = string x.Index

/// An action to be taken by the parser.
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`.
    | Shift of Indexed<LALRState>
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// This action is used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    | Goto of Indexed<LALRState>
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and complete.
    | Accept

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
and LALRState =
    {
        /// The index of the state.
        Index: uint32
        /// The available `LALRAction`s of the state.
        /// Depending on the symbol, the next action to be taken is determined.
        Actions:Map<Symbol, LALRAction>
    }
    override x.ToString() = string x.Index

/// Functions to work with `LALRState`s.
module internal LALRState =
    /// Returns all LALR actions to take when the corresponding symbol is encountered.
    let actions {Actions = actions} = actions

/// A structure that describes a grammar - the logic under which a string is parsed.
/// Its constructor is private; use functions like these from the `EGT` module to create one.
type GOLDGrammar =
        {
            _Properties: Properties
            _CharSets: (char * char) [] []
            _Symbols: Symbol []
            _Groups: Group []
            _Productions: Production []
            _LALR: StateTable<LALRState>
            _DFA: StateTable<DFAState>
        }

[<RequireQualifiedAccess>]
module GOLDGrammar =

    let counts (x: GOLDGrammar) =
        {
            SymbolTables = x._Symbols.Length |> uint16
            CharSetTables = x._CharSets.Length |> uint16
            ProductionTables = x._Productions.Length |> uint16
            DFATables = x._DFA.Length |> uint16
            LALRTables = x._LALR.Length |> uint16
            GroupTables = x._Groups.Length |> uint16
        }

    /// The `Properties` of the grammar.
    /// They are just metadata; they are not used by Farkle.
    let properties {_Properties = x} = x
    /// The `CharSet`s of the grammar.
    /// This might be removed in the future.
    let charSets {_CharSets = x} = x
    /// The `Symbol`s of the grammar.
    let symbols {_Symbols = x} = x
    /// The `Group`s of the grammar
    let groups {_Groups = x} = x
    /// The `Production`s of the grammar.
    let productions {_Productions = x} = x
    /// The grammar's LALR state table.
    let lalr {_LALR = x} = x
    /// The grammar's DFA state table.
    let dfa {_DFA = x} = x

    let create properties symbols charSets productions dfaStates lalrStates groups =
            {
                _Properties = properties
                _Symbols = symbols
                _CharSets = charSets
                _Productions = productions
                _DFA = dfaStates
                _LALR = lalrStates
                _Groups = groups
            }