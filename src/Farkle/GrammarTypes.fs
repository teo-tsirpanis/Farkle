// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.Types

open Chessie.ErrorHandling
open Farkle

type Indexed<'a> = Indexed<'a, uint16>

type GrammarError =
    | EGTReadError of EgtReader.EGTReadError
    | InvalidSymbolType of uint16
    | InvalidAdvanceMode of uint16
    | InvalidEndingMode of uint16
    | InvalidLALRActionType of uint16
    | IndexOutOfRange of uint16

type Properties = Properties of Map<string, string>

type TableCounts =
    {
        SymbolTable: uint16
        SetTable: uint16
        RuleTable: uint16
        DFATable: uint16
        LALRTable: uint16
        GroupTable: uint16
    }

type CharSet = RangeSet<char>

type SymbolType =
    | Nonterminal
    | Terminal
    | Noise
    | EndOfFile
    | GroupStart
    | GroundEnd
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
        | 5us -> ok GroundEnd
        // 6 is deprecated
        | 7us -> ok Error
        | x -> x |> InvalidSymbolType |> fail

type Symbol =
    {
        Name: string
        Kind: SymbolType
    }
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
        ContainerSymbol: Indexed<Symbol>
        StartSymbol: Indexed<Symbol>
        EndSymbol: Indexed<Symbol>
        AdvanceMode: AdvanceMode
        EndingMode: EndingMode
        Nesting: Set<Indexed<Group>>
    }

type Production =
    {
        Nonterminal: Indexed<Symbol>
        Symbols: Indexed<Symbol> list
    }

type InitialStates =
    {
        DFA: uint16
        LALR: uint16
    }

type DFAState =
    {
        AcceptState: Indexed<Symbol> option
        Edges: Set<Indexed<CharSet> * Indexed<DFAState>>
    }

type LALRState =
    {
        Actions: Map<Indexed<Symbol>, LALRActionType>
    }

and LALRActionType =
    | Shift of Indexed<LALRState>
    | Reduce of Indexed<Production>
    | Goto of Indexed<LALRState>
    | Accept

module LALRActionType =
    let create index =
        function
        | 1us -> index |> Indexed |> Shift |> ok
        | 2us -> index |> Indexed |> Reduce |> ok
        | 3us -> index |> Indexed |> Goto |> ok
        | 4us -> Accept |> ok
        | x -> x |> InvalidLALRActionType |> fail