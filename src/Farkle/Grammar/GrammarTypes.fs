// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle
open Farkle.EGTFile
open FSharpx.Collections
open System

/// A record that stores how many of each tables exist in an EGT file.
/// It's needed only for verifying that the grammar was successfuly read.
type internal TableCounts =
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

module internal Symbol =

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
        | _ -> fail UnknownEGTFile

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
        | _ -> fail UnknownEGTFile

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
        | _ -> fail UnknownEGTFile

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

type LALR =
    {
        InitialState: uint32
        States: Map<uint32, Map<Symbol, LALRAction>>
    }
    member x.Length = x.States.Count

module internal LALRAction =

    let create fProds index =
        function
        | 1us -> index |> Shift |> Ok
        | 2us -> index |> Indexed |> fProds |> Result.map Reduce
        | 3us -> index |> Goto |> Ok
        | 4us -> Accept |> Ok
        | _ -> fail UnknownEGTFile

/// A structure that describes a grammar - the logic under which a string is parsed.
/// Its constructor is private; use functions like these from the `EGT` module to create one.
type GOLDGrammar =
    {
        /// The `Properties` of the grammar.
        /// They are just metadata; they are not used by Farkle.
        Properties: Properties
        /// The `CharSet`s of the grammar.
        /// This might be removed in the future.
        CharSets: CharSet RandomAccessList
        /// The `Symbol`s of the grammar.
        Symbols: Symbol RandomAccessList
        /// The `Group`s of the grammar
        Groups: Group RandomAccessList
        /// The `Production`s of the grammar.
        Productions: Production RandomAccessList
        /// The grammar's LALR state table.
        LALR: LALR
        /// The grammar's DFA state table.
        DFA: DFA
    }

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

    let properties {Properties = x} = x
    let charSets {CharSets = x} = x
    let symbols {Symbols = x} = x
    let groups {Groups = x} = x
    let productions {Productions = x} = x
    let lalr {GOLDGrammar.LALR = x} = x
    let dfa {GOLDGrammar.DFA = x} = x

    let create properties symbols charSets productions dfaStates lalrStates groups tableCounts =
        let g =
            {
                Properties = properties
                Symbols = symbols
                CharSets = charSets
                Productions = productions
                DFA = dfaStates
                LALR = lalrStates
                Groups = groups
            }
        let counts = counts g
        if counts = tableCounts then
            Ok g
        else
            fail UnknownEGTFile
