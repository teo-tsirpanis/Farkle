// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

/// Functions used both by the legacy EGT and the EGTneo reader.
module internal Common =

    open Farkle.Grammar
    open System
    open System.Collections.Generic

    let aPosterioriConsistencyCheck (grammar: Grammar) =
        let terminals = grammar.Symbols.Terminals
        for i = 0 to terminals.Length - 1 do
            let (Terminal(termIdx, _)) = terminals.[i]
            if termIdx <> uint32 i then
                invalidEGTf "Terminal #%d is indexed out of order. It should be in position %d." termIdx i
        let nonterminals = grammar.Symbols.Nonterminals
        for i = 0 to nonterminals.Length - 1 do
            let (Nonterminal(nontIdx, _)) = nonterminals.[i]
            if nontIdx <> uint32 i then
                invalidEGTf "Nonterminal #%d is indexed out of order. It should be in position %d." nontIdx i
        let lalrStates = grammar.LALRStates
        for i = 0 to lalrStates.Length - 1 do
            match lalrStates.[i].EOFAction with
            | Some (LALRAction.Shift _) ->
                invalidEGTf "Error in LALR state %d: cannot shift when the end of input is encountered." i
            | _ -> ()

    let createProperties source (x: IReadOnlyDictionary<_,_>) =
        let name = x.GetOrDefault("Name", "")
        let caseSensitive =
            x.GetOrDefault("Case Sensitive", "true")
            |> Boolean.TryParse
            |> (fun (parsed, result) -> not parsed || result)
        let autoWhitespace =
            x.GetOrDefault("Auto Whitespace", "false")
            |> Boolean.TryParse
            |> snd
        let generator = x.GetOrDefault("Generated By", "")
        let generatedDate =
            x.GetOrDefault("Generated Date", "")
            |> DateTime.TryParse
            |> snd
        {
            Name = name
            CaseSensitive = caseSensitive
            AutoWhitespace = autoWhitespace
            GeneratedBy = generator
            GeneratedDate = generatedDate
            Source = source
        }

module internal EGTHeaders =

    // For better error messages only.
    let [<Literal>] CGTHeader = "GOLD Parser Tables/v1.0"

    let [<Literal>] EGTHeader = "GOLD Parser Tables/v5.0"

    // I initially wanted a more fancy header, one that was readable
    // in both Base64 and ASCII, perhaps loaded with easter eggs. But
    // I settled to this plain and boring header.
    let [<Literal>] EGTNeoHeader = "Farkle Parser Tables/v6.0"

    // The headers for each section of the EGTneo file.
    // They must be present in the file in that order.

    let [<Literal>] propertiesHeader = "Properties"
    let [<Literal>] terminalsHeader = "Terminals"
    let [<Literal>] nonterminalsHeader = "Nonterminals"
    let [<Literal>] noiseSymbolsHeader = "Noise Symbols"
    let [<Literal>] startSymbolHeader = "Start Symbol"
    let [<Literal>] groupsHeader = "Groups"
    let [<Literal>] productionsHeader = "Productions"
    let [<Literal>] lalrHeader = "LALR"
    let [<Literal>] dfaHeader = "DFA"
