// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.Dfa
open Farkle.Diagnostics
open Farkle.Diagnostics.Builder
open Farkle.Grammars
open Farkle.Grammars.Writers
open Farkle.Parser
open Farkle.Parser.Semantics
open System
open System.Buffers
open System.IO

let private terminalIndexSemanticProvider = {new ISemanticProvider<char, int> with
    member _.Transform (_, symbol, _) = symbol.Value
    member _.Fuse (_, _, members) = members[0]
}

/// Builds a grammar that matches either of the given regexes.
/// Until the LALR builder gets implemented, this function manually
/// constructs the LR state machine.
/// This is an advanced overload that also allows you to prioritize
/// symbols.
let buildSimpleRegexMatcherEx caseSensitive prioritizeSymbols regexes =
    let gw = GrammarWriter()
    let regexes = Array.ofList regexes
    let count = Array.length regexes
    let tokenSymbols = Array.init count (fun i -> gw.AddTokenSymbol(gw.GetOrAddString($"Token{i}"), TokenSymbolAttributes.Terminal))
    let rootNonterminal = gw.AddNonterminal(gw.GetOrAddString("S"), NonterminalAttributes.None, count)
    let productions = Array.init count (fun _ -> gw.AddProduction(1))
    Array.iter (TokenSymbolHandle.op_Implicit >> gw.AddProductionMember) tokenSymbols
    do
        // We need the initial state, the accepting state, and one state
        // for each token symbol to reduce the corresponding production.
        let lr = LrWriter(count + 2)
        Array.iteri (fun i x -> lr.AddShift(x, i + 2)) tokenSymbols
        lr.AddGoto(rootNonterminal, 1)
        lr.FinishState()
        lr.AddEofAccept()
        lr.FinishState()
        Array.iter (fun p -> lr.AddEofReduce(p); lr.FinishState()) productions
        gw.AddStateMachine lr
    let symbolsProvider = {new IGrammarSymbolsProvider with
        member _.SymbolCount = regexes.Length
        member _.GetRegex i = regexes[i]
        member _.GetTokenSymbolHandle i = tokenSymbols[i]
        member _.GetName i = BuilderSymbolName($"Token{i}", TokenSymbolKind.Terminal, false)}
    DfaBuild<char>.Build(symbolsProvider, caseSensitive, prioritizeSymbols, Int32.MaxValue)
    |> ValueOption.ofObj
    |> ValueOption.iter gw.AddStateMachine
    gw.SetGrammarInfo(gw.GetOrAddString("SimpleGrammar"), rootNonterminal, GrammarAttributes.None)
    gw.ToImmutableArray()
    |> Grammar.ofBytes
    |> CharParser.create terminalIndexSemanticProvider

/// Builds a grammar that matches either of the given regexes.
/// Until the LALR builder gets implemented, this function manually
/// constructs the LR state machine.
let buildSimpleRegexMatcher caseSensitive regexes =
    buildSimpleRegexMatcherEx caseSensitive false regexes

// It guarantees to work regardless of current directory.
// The resources folder is copied alongside with the executable.
let resourcesPath = AppContext.BaseDirectory |> Path.GetDirectoryName

let allEGTFiles =
    Directory.GetFiles(resourcesPath, "*.egt")
    |> List.ofArray

let getResourceFile fileName = Path.Combine(resourcesPath, fileName)

let loadGrammar (egtFile: string) =
    let resourceFile = getResourceFile egtFile
    match Path.GetExtension egtFile with
    | ".cgt" | ".egt" ->
        use stream = File.OpenRead resourceFile
        Grammar.ofGoldParserStream stream
    | _ ->
        Grammar.ofFile resourceFile

let loadCharParser egtFile =
    loadGrammar egtFile
    |> CharParser.createSyntaxCheck

let listOfSpan (span: ReadOnlySpan<_>) =
    let mutable list = []
    for i = span.Length - 1 downto 0 do
        list <- span[i] :: list
    list

let expectIsParseSuccess result msg =
    Expect.isOk (ParserResult.toResult result) msg

let expectWantParseSuccess result msg =
    Expect.wantOk (ParserResult.toResult result) msg

let expectIsParseFailure result msg =
    Expect.isError (ParserResult.toResult result) msg

let expectWantParseFailure result msg =
    Expect.wantError (ParserResult.toResult result) msg

let buildWithWarnings (grammarBuilder: IGrammarBuilder) =
    let diagnostics = ResizeArray()
    let builderOptions = BuilderOptions()
    builderOptions.LogLevel <- DiagnosticSeverity.Warning
    builderOptions.add_OnDiagnostic (fun x -> diagnostics.Add x)
    let grammar = grammarBuilder.BuildSyntaxCheck(builderOptions).GetGrammar()
    grammar, diagnostics

// Parses text with the given parser by feeding it one character at a time.
let parseGradual parser (text: string) =
    let ctx = ParserStateContext.Create parser
    let mutable i = 0
    while not ctx.IsCompleted && i < text.Length do
        ctx.Write(text.AsSpan().Slice(i, 1))
        i <- i + 1
    ctx.CompleteInput()
    ctx.Result
