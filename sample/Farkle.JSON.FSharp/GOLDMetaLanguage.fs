// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module GOLDMetaLanguage

open Farkle
open Farkle.Builder
open Farkle.Builder.Untyped

/// A `DesigntimeFarkle` that represents
/// the grammar for the GOLD Meta-Language.
let designtime =
    let cLiteral = Printable.Characters.Remove '\''
    let cParameter = cLiteral.Add '"'
    let cTerminal = AlphaNumeric.Characters + set "_-."
    let cNonterminal = cTerminal.Add ' '
    let cSetLiteral = cLiteral - set "[]"
    let cSetName = Printable.Characters - set "{}"

    let parameterName =
        [
            Regex.singleton '"'
            cParameter |> Regex.oneOf |> Regex.atLeast 1
            Regex.singleton '"'
        ] |> Regex.concat |> terminal "ParamaterName"
    let _nonterminal =
        [
            Regex.singleton '<'
            cNonterminal |> Regex.oneOf |> Regex.atLeast 1
            Regex.singleton '>'
        ] |> Regex.concat |> terminal "NonterminalName"
    let rLiteral =
        [
            Regex.singleton '\''
            cLiteral |> Regex.oneOf |> Regex.atLeast 0
            Regex.singleton '\''
        ] |> Regex.concat
    let _terminal =
        [
            cTerminal |> Regex.oneOf |> Regex.atLeast 1
            rLiteral
        ] |> Regex.choice |> terminal "Terminal"
    let setLiteral =
        [
            Regex.singleton '['
            [
                Regex.oneOf cSetLiteral
                [
                    Regex.singleton '\''
                    cLiteral |> Regex.oneOf |> Regex.atLeast 0
                    Regex.singleton '\''
                ] |> Regex.concat
            ] |> Regex.choice |> Regex.atLeast 1
            Regex.singleton ']'
        ] |> Regex.concat |> terminal "SetLiteral"
    let setName =
        [
            Regex.singleton '{'
            cSetName |> Regex.oneOf |> Regex.atLeast 1
            Regex.singleton '}'
        ] |> Regex.concat |> terminal "SetName"

    let nlOpt = nonterminal "nl opt"
    nlOpt.SetProductions([box newline; box nlOpt], [])
    let nl = nonterminal "nl"
    nl.SetProductions([box newline; box nl], [box newline])

    let parameter =
        let parameterItem =
            [parameterName; _terminal; setLiteral; setName; _nonterminal]
            |> List.map (box >> Seq.singleton)
            |> ((||=) "Parameter Item")

        let parameterItems = nonterminal "Parameter Items"
        parameterItems.SetProductions(
            [box parameterItems; box parameterItem],
            [box parameterItem])

        let parameterBody = nonterminal "Parameter Body"
        parameterBody.SetProductions(
            [box parameterBody; box nlOpt; box "|"; box parameterItems],
            [box parameterItems])
        "Parameter"
        ||= [[box parameterName; box nlOpt; box "="; box parameterBody; box nl]]

    let setDecl =
        let setItem =
            [setLiteral; setName]
            |> List.map (box >> Seq.singleton)
            |> ((||=) "Set Item")

        let setExp = nonterminal "Set Exp"
        setExp.SetProductions(
            [box setExp; box nlOpt; box "+"; box setItem],
            [box setExp; box nlOpt; box "-"; box setItem],
            [box setItem])

        "Set Decl"
        ||= [[box setName; box nlOpt; box "="; box setExp; box nl]]

    let terminalDecl =
        let kleeneOpt =
            Seq.empty :: (List.map (box >> Seq.singleton) ["+"; "?"; "*"])
            |> ((||=) "Kleene Opt")
        let regExp2 = nonterminal "Reg Exp 2"
        let regExpItem =
            [
                [box setLiteral]
                [box setName]
                [box _terminal]
                [box "("; box regExp2; box ")"]
            ]
            |> List.map (fun x -> Seq.append x [box kleeneOpt])
            |> ((||=) "Reg Exp Item")

        let regExpSeq = nonterminal "Reg Exp Seq"
        regExpSeq.SetProductions(
            [box regExpSeq; box regExpItem],
            [box regExpItem])
        // No newlines allowed
        regExp2.SetProductions(
            [box regExp2; box "|"; box regExpSeq],
            [box regExpSeq])

        let regExp = nonterminal "Reg Exp"
        regExp.SetProductions(
            [box regExp; box nlOpt; box "|"; box regExpSeq],
            [box regExpSeq])

        let terminalName = nonterminal "Terminal Name"
        terminalName.SetProductions(
            [box terminalName; box _terminal],
            [box _terminal])

        "Terminal Decl"
        ||= [[box terminalName; box nlOpt; box "="; box regExp; box nl]]

    let ruleDecl =
        let symbol =
            [_terminal; _nonterminal]
            |> List.map (box >> Seq.singleton)
            |> ((||=) "Symbol")

        let handle = nonterminal "Handle"
        handle.SetProductions([box handle; box symbol], [])

        let handles = nonterminal "Handles"
        handles.SetProductions(
            [box handles; box nlOpt; box "|"; box handle],
            [box handle])

        "Rule Decl"
        ||= [[box _nonterminal; box nlOpt; box "::="; box handles; box nl]]

    let definition =
        [parameter; setDecl; terminalDecl; ruleDecl]
        |> List.map (box >> Seq.singleton)
        |> ((||=) "Definition")

    let content = nonterminal "Content"
    content.SetProductions([box content; box definition], [box definition])

    "Grammar" ||= [[box nlOpt; box content]]

let runtime = RuntimeFarkle.buildUntyped designtime
