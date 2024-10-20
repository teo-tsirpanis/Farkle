// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Samples.FSharp.GOLDMetaLanguage

open Farkle.Builder

open Regex

/// An `IGrammarBuilder` that represents
/// the grammar for the GOLD Meta-Language.
let builder =
    let cTerminal = [struct ('0', '9'); 'A', 'Z'; 'a', 'z'; '_', '_'; '-', '-'; '.', '.']
    let cNonterminal = struct (' ', ' ') :: cTerminal

    let parameterName =
        [
            char '"'
            allButChars ['\''; '"'] |> atLeast 1
            char '"'
        ] |> Regex.concat |> terminalU "ParameterName"
    let _nonterminal =
        [
            char '<'
            cNonterminal |> charRanges |> atLeast 1
            char '>'
        ] |> Regex.concat |> terminalU "Nonterminal"
    let rLiteral =
        [
            char '\''
            allButChars ['\''] |> atLeast 0
            char '\''
        ] |> Regex.concat
    let _terminal =
        [
            cTerminal |> charRanges |> atLeast 1
            rLiteral
        ] |> Regex.choice |> terminalU "Terminal"
    let setLiteral =
        [
            char '['
            [
                allButChars ['\''; ']']
                [
                    char '\''
                    allButChars ['\''] |> atLeast 0
                    char '\''
                ] |> concat
            ] |> choice |> atLeast 1
            char ']'
        ] |> concat |> terminalU "SetLiteral"
    let setName =
        [
            char '{'
            allButChars ['{'; '}'] |> atLeast 1
            char '}'
        ] |> concat |> terminalU "SetName"

    let nlOpt = nonterminalU "nl opt"
    nlOpt.SetProductions(!% newline .>> nlOpt, empty)
    let nl = nonterminalU "nl"
    nl.SetProductions(!% newline .>> nl, !% newline)

    let parameter =
        let parameterItem =
            [parameterName; _terminal; setLiteral; setName; _nonterminal]
            |> List.map (!%)
            |> ((|||=) "Parameter Item")

        let parameterItems = nonterminalU "Parameter Items"
        parameterItems.SetProductions(
            !% parameterItems .>> parameterItem,
            !% parameterItem)

        let parameterBody = nonterminalU "Parameter Body"
        parameterBody.SetProductions(
            !% parameterBody .>> nlOpt .>> "|" .>> parameterItems,
            !% parameterItems)
        "Parameter"
        |||= [!% parameterName .>> nlOpt .>> "=" .>> parameterBody .>> nl]

    let setDecl =
        let setItem =
            [setLiteral; setName]
            |> List.map (!%)
            |> ((|||=) "Set Item")

        let setExp = nonterminalU "Set Exp"
        setExp.SetProductions(
            !% setExp .>> nlOpt .>> "+" .>> setItem,
            !% setExp .>> nlOpt .>> "-" .>> setItem,
            !% setItem)

        "Set Decl"
        |||= [!% setName .>> nlOpt .>> "=" .>> setExp .>> nl]

    let terminalDecl =
        let kleeneOpt =
            empty :: (List.map (!&) ["+"; "?"; "*"])
            |> ((|||=) "Kleene Opt")
        let regExp2 = nonterminalU "Reg Exp 2"
        let regExpItem =
            [
                !% setLiteral
                !% setName
                !% _terminal
                !& "(" .>> regExp2 .>> ")"
            ]
            |> List.map (fun x -> x .>> kleeneOpt)
            |> ((|||=) "Reg Exp Item")

        let regExpSeq = nonterminalU "Reg Exp Seq"
        regExpSeq.SetProductions(
            !% regExpSeq .>> regExpItem,
            !% regExpItem)
        // No newlines allowed
        regExp2.SetProductions(
            !% regExp2 .>> "|" .>> regExpSeq,
            !% regExpSeq)

        let regExp = nonterminalU "Reg Exp"
        regExp.SetProductions(
            !% regExp .>> nlOpt .>> "|" .>> regExpSeq,
            !% regExpSeq)

        let terminalName = nonterminalU "Terminal Name"
        terminalName.SetProductions(
            !% terminalName .>> _terminal,
            !% _terminal)

        "Terminal Decl"
        |||= [!% terminalName .>> nlOpt .>> "=" .>> regExp .>> nl]

    let ruleDecl =
        let symbol =
            [_terminal; _nonterminal]
            |> List.map (!%)
            |> ((|||=) "Symbol")

        let handle = nonterminalU "Handle"
        handle.SetProductions(!% handle .>> symbol, empty)

        let handles = nonterminalU "Handles"
        handles.SetProductions(
            !% handles .>> nlOpt .>> "|" .>> handle,
            !% handle)

        "Rule Decl"
        |||= [!%_nonterminal .>> nlOpt .>> "::=" .>> handles .>> nl]

    let definition =
        [parameter; setDecl; terminalDecl; ruleDecl]
        |> List.map (!%)
        |> ((|||=) "Definition")

    let content = nonterminalU "Content"
    content.SetProductions(!% content .>> definition, !% definition)

    "Grammar" |||= [!% nlOpt .>> content]
    |> _.AddBlockComment("!*", "*!")
    |> _.AddLineComment("!")
    |> _.NewLineIsNoisy(false)

let parser = GrammarBuilder.buildSyntaxCheck builder
