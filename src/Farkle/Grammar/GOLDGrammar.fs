// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open Farkle.EGTFile
open Farkle.Grammar
open System.Collections.Immutable

/// A structure that describes a grammar - the logic under which a string is parsed.
/// Its constructor is private; use functions like these from the `EGT` module to create one.
type GOLDGrammar =
    internal
        {
            _Properties: Properties
            _CharSets: CharSet ImmutableArray
            _Symbols: Symbol ImmutableArray
            _Groups: Group ImmutableArray
            _Productions: Production ImmutableArray
            _LALR: LALR
            _DFA: DFA
        }
    interface RuntimeGrammar with
        member x.DFA = x._DFA
        member x.Groups = x._Groups
        member x.LALR = x._LALR

[<RequireQualifiedAccess>]
module GOLDGrammar =

    let private counts (x: GOLDGrammar) =
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

    let internal create properties symbols charSets productions dfaStates lalrStates groups tableCounts =
        let g =
            {
                _Properties = properties
                _Symbols = symbols
                _CharSets = charSets
                _Productions = productions
                _DFA = dfaStates
                _LALR = lalrStates
                _Groups = groups
            }
        let counts = counts g
        if counts = tableCounts then
            Ok g
        else
            fail UnknownEGTFile