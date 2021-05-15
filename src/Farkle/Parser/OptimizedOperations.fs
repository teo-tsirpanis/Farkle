// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Collections
open Farkle.Grammar
open System.Collections.Immutable
open System.Runtime.CompilerServices

[<Struct>]
/// An value type representing a DFA state or its absence.
/// It is returned from optimized operations.
type internal DFAStateTag = private DFAStateTag of int
with
    /// Creates a successful `DFAStateTag`.
    static member internal Ok (x: uint32) = DFAStateTag <| int x
    static member InitialState = 0
    /// A failed `DFAStateTag`.
    static member Error = DFAStateTag -1
    static member internal FromOption x =
        match x with
        | Some x -> DFAStateTag.Ok x
        | None -> DFAStateTag.Error
    /// The state number of this `DFAStateTag`,
    /// or a negative number.
    member x.Value = match x with DFAStateTag x -> x
    /// Whether this `DFAStateTag` represents a successful operation.
    member x.IsOk = x.Value >= 0
    /// Whether this `DFAStateTag` represents a failed operation.
    member x.IsError = x.Value < 0

[<AutoOpen>]
module private OptimizedOperations =

    [<Literal>]
    /// The number of ASCII characters.
    let ASCIICharacterCount = 128

    /// Checks if the given character belongs to ASCII.
    /// The first control characters are included.
    let inline isASCII c = c < char ASCIICharacterCount

    // We use a shared dummy array if a DFA state does not have any
    // edges from an ASCII character, or Anything Else.
    let private dfaStateAllErrors = Array.create ASCIICharacterCount DFAStateTag.Error

    let private createJaggedArray2D length1 length2 =
        let arr = Array.zeroCreate length1
        for i = 0 to length1 - 1 do
            arr.[i] <- Array.zeroCreate length2
        arr

    let private rangeMapToSeq xs = RangeMap.toSeqEx xs

    /// Creates a two-dimensional array of DFA state indices, whose first dimension
    /// represents the index of the current DFA state, and the second represents the
    /// ASCII character that was encountered.
    let buildDFAArray (dfa: ImmutableArray<DFAState>) =
        let arr = Array.zeroCreate dfa.Length
        for i = 0 to dfa.Length - 1 do
            let state = dfa.[i]
            let failsOnAllAscii =
                state.AnythingElse.IsNone
                && (state.Edges.Elements.IsEmpty || not (isASCII state.Edges.Elements.[0].KeyFrom))
            arr.[i] <-
                if failsOnAllAscii then
                    dfaStateAllErrors
                else
                    let arr = Array.zeroCreate ASCIICharacterCount
                    let anythingElse = DFAStateTag.FromOption state.AnythingElse
                    // TODO: Use Array.Fill when .NET Standard 2.0 support is dropped.
                    for j = 0 to arr.Length - 1 do
                        arr.[j] <- anythingElse
                    let chars =
                        state.Edges
                        |> rangeMapToSeq
                        |> Seq.takeWhile (fun x -> isASCII x.Key)
                    for KeyValue(c, state) in chars do
                        arr.[int c] <- DFAStateTag.FromOption state
                    arr
        arr

    let buildDFAAnythingElseArray (dfa: ImmutableArray<DFAState>) =
        let arr = Array.zeroCreate dfa.Length
        for i = 0 to arr.Length - 1 do
            arr.[i] <- DFAStateTag.FromOption dfa.[i].AnythingElse
        arr

    let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        let maxTerminalIndex =
            if terminals.IsEmpty then
                0
            else
                terminals |> Seq.map(fun (Terminal(idx, _)) -> idx) |> Seq.max |> int
        let arr = createJaggedArray2D lalr.Length (maxTerminalIndex + 1)
        for {Index = stateIndex; Actions = actions} in lalr do
            for KeyValue(Terminal(idx, _), action) in actions do
                arr.[int stateIndex].[int idx] <- Some action
        arr

    let buildLALRGotoArray (nonterminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // There is no way for a grammar to lack nonterminals.
        let maxNonterminalIndex = nonterminals |> Seq.map(fun (Nonterminal(idx, _)) -> idx) |> Seq.max |> int
        // No reason to allocate many options.
        let lalrOptions = lalr |> Seq.map Some |> Array.ofSeq
        let arr = createJaggedArray2D lalr.Length (maxNonterminalIndex + 1)
        for {Index = stateIndex; GotoActions = gotoActions} in lalr do
            let stateIndex = int stateIndex
            for KeyValue(Nonterminal(nontIdx, _), idx) in gotoActions do
                arr.[stateIndex].[int nontIdx] <- lalrOptions.[int idx]
        arr

/// An object that provides optimized functions for some common operations on Grammars.
/// These functions require some memory-intensive
/// pre-processing, which is performed only once per grammar.
type internal OptimizedOperations private(grammar: Grammar) =
    // I love this type. It surprisingly simply allows
    // for some very efficient decoupled object caches.
    // It's also fast and thread-safe. Getting a value
    // from a CWT does not lock unless the key does not exist.
    static let cache = ConditionalWeakTable()
    static let cacheItemCreator = ConditionalWeakTable.CreateValueCallback OptimizedOperations

    let dfaArray = buildDFAArray grammar.DFAStates
    let dfaAnythingElseArray = buildDFAAnythingElseArray grammar.DFAStates
    let lalrActionArray = buildLALRActionArray grammar.Symbols.Terminals grammar.LALRStates
    let lalrGotoArray = buildLALRGotoArray grammar.Symbols.Nonterminals grammar.LALRStates
    /// Returns an `OptimizedOperations` object associated
    /// with the given `Grammar`. Multiple invocations of
    /// this method with the same grammar might return the
    /// same instance which is collected with the grammar.
    static member Create grammar = cache.GetValue(grammar, cacheItemCreator)
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    member _.GetNextDFAState c state =
        let stateArray = dfaArray.[state]
        if int c < stateArray.Length then
            stateArray.[int c]
        else
            match grammar.DFAStates.[state].Edges.TryFind c with
            | ValueSome x -> DFAStateTag.FromOption x
            | ValueNone -> dfaAnythingElseArray.[state]
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    member _.GetLALRAction (Terminal (term, _)) {LALRState.Index = idx} =
        lalrActionArray.[int idx].[int term]
    /// Gets the next LALR state according to the given state's GOTO actions.
    member _.LALRGoto (Nonterminal (nont, _)) {LALRState.Index = idx} =
        lalrGotoArray.[int idx].[int nont]
