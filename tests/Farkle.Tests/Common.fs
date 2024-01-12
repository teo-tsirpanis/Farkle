// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tests.Common

open Expecto
open Farkle
open Farkle.Builder.StateMachines
open Farkle.Grammars
open Farkle.Grammars.Writers
open Farkle.Parser.Semantics
open System
open System.IO

let private terminalIndexSemanticProvider = {new ISemanticProvider<char, int> with
    member _.Transform (_, symbol, _) = symbol.Value
    member _.Fuse (_, _, members) = members[0]
}

/// Builds a grammar that matches either of the given regexes.
/// Until the LALR builder gets implemented, this function manually
/// constructs the LR state machine.
/// This is an advanced overload that also allows you to prioritize
/// fixed-length symbols.
let buildSimpleRegexMatcherEx caseSensitive prioritizeFixedLengthSymbols regexes =
    let count = List.length regexes
    let gw = GrammarWriter()
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
    (regexes, tokenSymbols)
    ||> Seq.map2 (fun r t -> struct (r, t, ""))
    |> Array.ofSeq
    |> fun x -> DfaBuild<char>.Build(x, caseSensitive, prioritizeFixedLengthSymbols, Int32.MaxValue)
    |> gw.AddStateMachine
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
