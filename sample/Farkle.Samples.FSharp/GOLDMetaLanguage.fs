// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module GOLDMetaLanguage

open Farkle
open Farkle.Builder
open Farkle.Builder.Untyped
open System.Collections.Immutable

open Regex

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
            char '"'
            cParameter |> chars |> atLeast 1
            char '"'
        ] |> Regex.concat |> terminal "ParameterName"
    let _nonterminal =
        [
            char '<'
            cNonterminal |> chars |> atLeast 1
            char '>'
        ] |> Regex.concat |> terminal "Nonterminal"
    let rLiteral =
        [
            char '\''
            cLiteral |> chars |> atLeast 0
            char '\''
        ] |> Regex.concat
    let _terminal =
        [
            cTerminal |> chars |> atLeast 1
            rLiteral
        ] |> Regex.choice |> terminal "Terminal"
    let setLiteral =
        [
            char '['
            [
                chars cSetLiteral
                [
                    char '\''
                    cLiteral |> chars |> atLeast 0
                    char '\''
                ] |> concat
            ] |> choice |> atLeast 1
            char ']'
        ] |> concat |> terminal "SetLiteral"
    let setName =
        [
            char '{'
            cSetName |> chars |> atLeast 1
            char '}'
        ] |> concat |> terminal "SetName"

    let nlOpt = nonterminal "nl opt"
    nlOpt.SetProductions(!% newline .>> nlOpt, empty)
    let nl = nonterminal "nl"
    nl.SetProductions(!% newline .>> nl, !% newline)

    let parameter =
        let parameterItem =
            [parameterName; _terminal; setLiteral; setName; _nonterminal]
            |> List.map (!%)
            |> ((||=) "Parameter Item")

        let parameterItems = nonterminal "Parameter Items"
        parameterItems.SetProductions(
            !% parameterItems .>> parameterItem,
            !% parameterItem)

        let parameterBody = nonterminal "Parameter Body"
        parameterBody.SetProductions(
            [box parameterBody; box nlOpt; box "|"; box parameterItems],
            [box parameterItems])
        "Parameter"
        ||= [!% parameterName .>> nlOpt .>> "=" .>> parameterBody .>> nl]

    let setDecl =
        let setItem =
            [setLiteral; setName]
            |> List.map (!%)
            |> ((||=) "Set Item")

        let setExp = nonterminal "Set Exp"
        setExp.SetProductions(
            [box setExp; box nlOpt; box "+"; box setItem],
            [box setExp; box nlOpt; box "-"; box setItem],
            [box setItem])

        "Set Decl"
        ||= [!% setName .>> nlOpt .>> "=" .>> setExp .>> nl]

    let terminalDecl =
        let kleeneOpt =
            empty :: (List.map (!&) ["+"; "?"; "*"])
            |> ((||=) "Kleene Opt")
        let regExp2 = nonterminal "Reg Exp 2"
        let regExpItem =
            [
                !% setLiteral
                !% setName
                !% _terminal
                !& "(" .>> regExp2 .>> ")"
            ]
            |> List.map (fun x -> x .>> kleeneOpt)
            |> ((||=) "Reg Exp Item")

        let regExpSeq = nonterminal "Reg Exp Seq"
        regExpSeq.SetProductions(
            !% regExpSeq .>> regExpItem,
            !% regExpItem)
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
            !% terminalName .>> _terminal,
            !% _terminal)

        "Terminal Decl"
        ||= [!% terminalName .>> nlOpt .>> "=" .>> regExp .>> nl]

    let ruleDecl =
        let symbol =
            [_terminal; _nonterminal]
            |> List.map (!%)
            |> ((||=) "Symbol")

        let handle = nonterminal "Handle"
        handle.SetProductions(!% handle .>> symbol, empty)

        let handles = nonterminal "Handles"
        // Cannot use production builders due to
        // https://github.com/dotnet/fsharp/issues/7917
        handles.SetProductions(
            [box handles; box nlOpt; box "|"; box handle],
            [box handle])

        "Rule Decl"
        ||= [!%_nonterminal .>> nlOpt .>> "::=" .>> handles .>> nl]

    let definition =
        [parameter; setDecl; terminalDecl; ruleDecl]
        |> List.map (!%)
        |> ((||=) "Definition")

    let content = nonterminal "Content"
    content.SetProductions(!% content .>> definition, !% definition)

    let metadata = {
        GrammarMetadata.Default with
            Comments = ImmutableList.Create(BlockComment("!*", "*!"), LineComment "!")
    }

    "Grammar" ||= [!% nlOpt .>> content]
    |> DesigntimeFarkle.withMetadataUntyped metadata

let runtime = RuntimeFarkle.buildUntyped designtime
