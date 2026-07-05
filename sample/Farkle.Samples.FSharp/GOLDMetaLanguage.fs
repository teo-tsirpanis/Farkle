// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Samples.FSharp.GOLDMetaLanguage

open Farkle.Builder
open FSharp.Core.CompilerServices
open System
open System.Collections.Immutable
open System.Text

open Regex

type private SymbolReference =
    | Literal of string
    | Terminal of string
    | Nonterminal of string

let private getSymbolContent =
    function
    | Literal x -> x
    | Terminal x -> x
    | Nonterminal x -> x

type private CharSetReference =
    | SetName of string
    | SetLiteral of char Set
    | SetUnion of CharSetReference * CharSetReference
    | SetSubtraction of CharSetReference * CharSetReference

type private ParameterValue =
    | ParameterString of string
    | ParameterSymbol of SymbolReference
    | ParameterSetName of string
    | ParameterSetLiteral of char Set

[<RequireQualifiedAccess>]
type private RegexDefinition =
    | CharSet of CharSetReference
    | Literal of string
    | Star of RegexDefinition
    | Plus of RegexDefinition
    | Optional of RegexDefinition
    | Concat of RegexDefinition * RegexDefinition
    | Alt of RegexDefinition * RegexDefinition

type private Production = SymbolReference list

[<RequireQualifiedAccess>]
type private Definition =
    | Parameter of string * ParameterValue
    | CharSet of string * CharSetReference
    | Terminal of string * RegexDefinition
    | Nonterminal of string * Production list

type private GrammarDefinition = {
    Name: string option
    CaseSensitive: bool option
    StartSymbol: string option

    CharSets: Map<string, CharSetReference>
    Terminals: Map<string, RegexDefinition>
    Nonterminals: Map<string, Production list>
}

let private emptyGrammarDefinition = {
    Name = None
    CaseSensitive = None
    StartSymbol = None
    CharSets = Map.empty
    Terminals = Map.empty
    Nonterminals = Map.empty
}

let private getPredefinedSet (name: string) =
    match name.ToLowerInvariant() with
    | "printable" -> set <| ['\x20'..'\x7E'] @['\xA0']
    | "alphanumeric" -> set <| ['0'..'9'] @ ['A'..'Z'] @ ['a'..'z']
    | "whitespace" -> set [' '; '\t'; '\n'; '\r']
    | "cr" -> set ['\r']
    | "lf" -> set ['\n']
    | "space" -> set [' ']
    // We support only as many predefined sets as are used in the sample grammars attached in the repository.
    | _ -> errorf "Unknown character set '%s'." name

let private appendGrammarDefinition grammar definition =
    match definition with
    | Definition.Parameter("Name", ParameterString value) when grammar.Name.IsNone -> { grammar with Name = Some value }
    | Definition.Parameter("Case Sensitive", ParameterString value) when grammar.CaseSensitive.IsNone ->
        let caseSensitive =
            match value.ToLowerInvariant() with
            | "true" -> Some true
            | "false" -> Some false
            | _ -> None
        { grammar with CaseSensitive = caseSensitive }
    | Definition.Parameter("Start Symbol", ParameterSymbol(Nonterminal value)) when grammar.StartSymbol.IsNone ->
        { grammar with StartSymbol = Some value }
    | Definition.Parameter _ -> grammar

    | Definition.CharSet(name, charSet) ->
        { grammar with CharSets = Map.add name charSet grammar.CharSets }

    | Definition.Terminal(name, regex) ->
        if Map.containsKey name grammar.Terminals then
            error "Terminal already defined"
        { grammar with Terminals = Map.add name regex grammar.Terminals }

    | Definition.Nonterminal(name, productions) ->
        let productions =
            match Map.tryFind name grammar.Nonterminals with
            | Some existingProductions -> existingProductions @ productions
            | None -> productions

        { grammar with Nonterminals = Map.add name productions grammar.Nonterminals }

let private buildGrammarDefinition (grammar: GrammarDefinition) =
    let charSets = Collections.Generic.Dictionary<string, char Set> StringComparer.OrdinalIgnoreCase
    let visited = Collections.Generic.HashSet<string> StringComparer.OrdinalIgnoreCase

    let rec resolveCharSetReference =
        function
        | SetLiteral x -> x
        | SetUnion(x1, x2) -> resolveCharSetReference x1 + resolveCharSetReference x2
        | SetSubtraction(x1, x2) -> resolveCharSetReference x1 - resolveCharSetReference x2
        | SetName name ->
            match charSets.TryGetValue name with
            | true, x -> x
            | false, _ ->
                if not (visited.Add name) then
                    errorf "Character set '%s' is recursive." name

                let x =
                    match Map.tryFind name grammar.CharSets with
                    | Some x -> x |> resolveCharSetReference
                    | None -> getPredefinedSet name

                visited.Remove name |> ignore
                charSets.Add(name, x)
                x

    let rec buildRegex =
        function
        | RegexDefinition.CharSet x -> x |> resolveCharSetReference |> Regex.chars
        | RegexDefinition.Literal x -> Regex.string x
        | RegexDefinition.Star x -> x |> buildRegex |> Regex.star
        | RegexDefinition.Plus x -> x |> buildRegex |> Regex.plus
        | RegexDefinition.Optional x -> x |> buildRegex |> Regex.optional
        | RegexDefinition.Concat(x1, x2) -> buildRegex x1 + buildRegex x2
        | RegexDefinition.Alt(x1, x2) -> buildRegex x1 ||| buildRegex x2

    let terminals =
        grammar.Terminals
        |> Map.map (fun name regex -> regex |> buildRegex |> terminalU name)

    let nonterminals =
        grammar.Nonterminals
        |> Map.map (fun key prods -> nonterminalU key, prods)

    let getSymbol =
        function
        | Literal x -> literal x
        | Terminal x ->
            match Map.tryFind x terminals with
            | Some symbol -> symbol
            | None -> literal x
        | Nonterminal x ->
            match Map.tryFind x nonterminals with
            | Some (symbol, _) -> symbol
            | None -> errorf "Unknown nonterminal '%s'." x

    let buildProduction (production: Production) =
        production
        |> Seq.map (getSymbol >> box)
        |> Array.ofSeq
        |> fun x -> ProductionBuilder x
        // |> List.fold (fun pb symbolRef -> pb .>> getSymbol symbolRef) empty

    nonterminals
    |> Map.iter (fun _ (nonterminal, productions) ->
        productions
        |> Seq.map buildProduction
        |> ImmutableArray.CreateRange
        |> nonterminal.SetProductions
    )

    let startSymbolName =
        match grammar.StartSymbol with
        | Some name -> name
        | None -> error "The StartSymbol parameter must be specified."

    let mutable builder =
        match Map.tryFind startSymbolName nonterminals with
        | Some (nonterminal, _) -> nonterminal :> IGrammarBuilder
        | None -> errorf "Unknown start symbol '%s'." startSymbolName

    match grammar.Name with
    | Some name -> builder <- builder.WithGrammarName name
    | None -> ()

    match grammar.CaseSensitive with
    | Some caseSensitive -> builder <- builder.CaseSensitive caseSensitive
    | None -> ()

    builder

let private makeListCollector x =
    // ListCollector is a mutable struct.
    let mutable xs = ListCollector<_>()
    xs.Add x
    xs

let private addListCollector (xs: ListCollector<_>) x =
    // ListCollector is a mutable struct.
    let mutable xs = xs
    xs.Add x
    xs

let private closeListCollector (xs: ListCollector<_>) = xs.Close()

let unescapeLiteralString (s: ReadOnlySpan<char>) =
    let sb = StringBuilder s.Length
    let mutable i = 0
    while i < s.Length do
        match s[i] with
        | '\'' when s[i + 1] = '\'' ->
            sb.Append '\'' |> ignore
            i <- i + 2
        | '\'' ->
            i <- i + 1
            let s = s.Slice i
            let s = s.Slice(0, s.IndexOf '\'')
            for c in s do sb.Append c |> ignore
            i <- i + s.Length + 1
        | c ->
            sb.Append c |> ignore
            i <- i + 1
    sb.ToString()

/// An `IGrammarBuilder` that represents
/// the grammar for the GOLD Meta-Language.
let builder =
    let cTerminal = [struct ('0', '9'); 'A', 'Z'; 'a', 'z'; '_', '_'; '-', '-'; '.', '.']
    let cNonterminal = struct (' ', ' ') :: cTerminal

    let tQuotedString = T(fun _ chars -> chars.Slice(1, chars.Length - 2).ToString())

    let parameterName =
        [
            char '"'
            allButChars ['\''; '"'] |> atLeast 1
            char '"'
        ] |> Regex.concat |> terminal "ParameterName" tQuotedString
    let _nonterminal =
        [
            char '<'
            cNonterminal |> charRanges |> atLeast 1
            char '>'
        ] |> Regex.concat |> terminal "Nonterminal" tQuotedString
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
        ] |> Regex.choice |> terminal "Terminal" (T(fun _ data ->
            if data[0] = '\'' then
                let data = data.Slice(1, data.Length - 2)
                if data.IsEmpty then // '' in GML means the single quote character
                    "'"
                else
                    unescapeLiteralString data
                |> Literal
            else
                data.ToString()
                |> Terminal))
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
        ] |> concat |> terminal "SetLiteral" (T(fun _ data ->
            let data = data.Slice(1, data.Length - 2)
            unescapeLiteralString data
            |> set))
    let setName =
        [
            char '{'
            allButChars ['{'; '}'] |> atLeast 1
            char '}'
        ] |> concat |> terminal "SetName" tQuotedString

    let nlOpt = nonterminalU "nl opt"
    setProductionsU nlOpt [!% newline .>> nlOpt; empty]
    let nl = nonterminalU "nl"
    setProductionsU nl [!% newline .>> nl; !% newline]

    let parameter =
        let parameterItem =
            "Parameter Item" ||= [
                !@ parameterName => ParameterString
                !@ _terminal => ParameterSymbol
                !@ setLiteral => ParameterSetLiteral
                !@ setName => ParameterSetName
                !@ _nonterminal => (Nonterminal >> ParameterSymbol)
            ]

        let parameterItems = nonterminal "Parameter Items"
        setProductions parameterItems [
            !@ parameterItems .>> parameterItem |> asProduction // Ignore subsequent items
            !@ parameterItem |> asProduction
        ]

        let parameterBody = nonterminal "Parameter Body"
        setProductions parameterBody [
            !@ parameterBody .>> nlOpt .>> "|" .>> parameterItems |> asProduction // Ignore subsequent items
            !@ parameterItems |> asProduction
        ]
        "Parameter" ||= [
            !@ parameterName .>> nlOpt .>> "=" .>>. parameterBody .>> nl => fun name x -> Definition.Parameter(name, x)
        ]

    let setDecl =
        let setItem =
            "Set Item" ||= [
                !@ setLiteral => SetLiteral
                // GOLD Parser also supports character constants and ranges like {&1db .. &29e}, but we don't need them here.
                !@ setName => SetName
            ]

        let setExp = nonterminal "Set Exp"
        setProductions setExp [
            !@ setExp .>> nlOpt .>> "+" .>>. setItem => fun x1 x2 -> SetUnion(x1, x2)
            !@ setExp .>> nlOpt .>> "-" .>>. setItem => fun x1 x2 -> SetSubtraction(x1, x2)
            !@ setItem |> asProduction
        ]

        "Set Decl" ||= [
            !@ setName .>> nlOpt .>> "=" .>>. setExp .>> nl => fun name x -> Definition.CharSet(name, x)
        ]

    let terminalDecl =
        let kleeneOpt =
            "Kleene Opt" ||= [
                empty =% id
                !& "+" =% RegexDefinition.Plus
                !& "?" =% RegexDefinition.Optional
                !& "*" =% RegexDefinition.Star
            ]
        let regExp2 = nonterminal "Reg Exp 2"
        let regExpItem =
            "Reg Exp Item" ||= [
                !@ setLiteral .>>. kleeneOpt => fun x f -> x |> SetLiteral |> RegexDefinition.CharSet |> f
                !@ setName .>>. kleeneOpt => fun x f -> x |> SetName |> RegexDefinition.CharSet |> f
                !@ _terminal .>>. kleeneOpt => fun x f -> x |> getSymbolContent |> RegexDefinition.Literal |> f
                !& "(" .>>. regExp2 .>> ")" .>>. kleeneOpt => (|>)
            ]

        let regExpSeq = nonterminal "Reg Exp Seq"
        setProductions regExpSeq [
            !@ regExpSeq .>>. regExpItem => fun x1 x2 -> RegexDefinition.Concat(x1, x2)
            !@ regExpItem |> asProduction
        ]
        // No newlines allowed
        setProductions regExp2 [
            !@ regExp2 .>> "|" .>>. regExpSeq => fun x1 x2 -> RegexDefinition.Alt(x1, x2)
            !@ regExpSeq |> asProduction
        ]

        let regExp = nonterminal "Reg Exp"
        setProductions regExp [
            !@ regExp .>> nlOpt .>> "|" .>>. regExpSeq => fun x1 x2 -> RegexDefinition.Alt(x1, x2)
            !@ regExpSeq |> asProduction
        ]

        let terminalName = nonterminal "Terminal Name"
        setProductions terminalName [
            // This actually happens when defining groups. It's not necessary to properly support groups right now.
            !@ terminalName .>>. _terminal => fun name t -> $"{name} {getSymbolContent t}"
            !@ _terminal => getSymbolContent
        ]

        "Terminal Decl" ||= [
            !@ terminalName .>> nlOpt .>> "=" .>>. regExp .>> nl => fun name x -> Definition.Terminal(name, x)
        ]

    let ruleDecl =
        let symbol =
            "Symbol" ||= [
                !@ _terminal |> asProduction
                !@ _nonterminal => Nonterminal
            ]

        let handle = nonterminal "Handle"
        setProductions handle [
            !@ handle .>>. symbol => addListCollector
            empty => ListCollector<_>
        ]

        let handles = nonterminal "Handles"
        setProductions handles [
            !@ handles .>> nlOpt .>> "|" .>>. handle => fun xs x -> x |> closeListCollector |> addListCollector xs
            !@ handle => (closeListCollector >> makeListCollector)
        ]

        "Rule Decl" ||= [
            !@ _nonterminal .>> nlOpt .>> "::=" .>>. handles .>> nl => fun name xs -> Definition.Nonterminal(name, xs.Close())
        ]

    let definition =
        [parameter; setDecl; terminalDecl; ruleDecl]
        |> List.map ((!@) >> asProduction)
        |> (||=) "Definition"

    let content = nonterminal "Content"
    setProductions content [
        !@ content .>>. definition => appendGrammarDefinition
        !@ definition => appendGrammarDefinition emptyGrammarDefinition
    ]

    "Grammar" ||= [!% nlOpt .>>. content => buildGrammarDefinition]
    |> _.AddBlockComment("!*", "*!")
    |> _.AddLineComment("!")
    |> _.NewLineIsNoisy(false)
    |> _.WithGrammarName("GOLD Meta-Language")

let parser = GrammarBuilder.build builder
