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
    static member InitialState = DFAStateTag 0
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
    /// The ASCII character with the highest code point (~).
    let ASCIIUpperBound = 127

    /// Checks if the given character belongs to ASCII.
    /// The first control characters are included.
    let inline isASCII c = c <= char ASCIIUpperBound

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
        let arr = createJaggedArray2D dfa.Length (ASCIIUpperBound + 2)
        dfa
        |> Seq.iteri (fun i state ->
            let anythingElse = DFAStateTag.FromOption dfa.[i].AnythingElse
            for j = 0 to ASCIIUpperBound + 1 do
                arr.[i].[j] <- anythingElse
            state.Edges
            |> rangeMapToSeq
            |> Seq.takeWhile (fun x -> isASCII x.Key)
            |> Seq.iter (fun x ->
                arr.[i].[int x.Key] <- DFAStateTag.FromOption x.Value))
        arr

    let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        let maxTerminalIndex =
            if terminals.IsEmpty then
                0
            else
                terminals |> Seq.map(fun (Terminal(idx, _)) -> idx) |> Seq.max |> int
        let arr = createJaggedArray2D lalr.Length (maxTerminalIndex + 1)
        lalr
        |> Seq.iter (fun {Index = stateIndex; Actions = actions} ->
            actions
            |> Seq.iter (fun (KeyValue(term, action)) ->
                arr.[int stateIndex].[int term.Index] <- Some action))
        arr

    let buildLALRGotoArray (nonterminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // There is no way for a grammar to lack nonterminals.
        let maxNonterminalIndex = nonterminals |> Seq.map(fun (Nonterminal(idx, _)) -> idx) |> Seq.max |> int
        // No reason to allocate many options.
        let lalrOptions = lalr |> Seq.map Some |> Array.ofSeq
        let arr = createJaggedArray2D lalr.Length (maxNonterminalIndex + 1)
        lalr
        |> Seq.iter (fun {Index = stateIndex; GotoActions = actions} ->
            let stateIndex = int stateIndex
            actions
            |> Seq.iter (fun (KeyValue(nont, idx)) ->
                arr.[stateIndex].[int nont.Index] <- lalrOptions.[int idx]))
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
    let lalrActionArray = buildLALRActionArray grammar.Symbols.Terminals grammar.LALRStates
    let lalrGotoArray = buildLALRGotoArray grammar.Symbols.Nonterminals grammar.LALRStates
    /// Returns an `OptimizedOperations` object associated
    /// with the given `Grammar`. Multiple invocations of
    /// this method with the same grammar might return the
    /// same instance which is collected with the grammar.
    static member Create grammar = cache.GetValue(grammar, cacheItemCreator)
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    member _.GetNextDFAState c (state: DFAStateTag) =
        let arr = dfaArray
        if state.IsError then
            DFAStateTag.Error
        elif isASCII c then
            arr.[state.Value].[int c]
        else
            match grammar.DFAStates.[state.Value].Edges.TryFind c with
            | ValueSome x -> DFAStateTag.FromOption x
            | ValueNone -> arr.[state.Value].[ASCIIUpperBound + 1]
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    member _.GetLALRAction (Terminal (term, _)) {LALRState.Index = idx} =
        lalrActionArray.[int idx].[int term]
    /// Gets the next LALR state according to the given state's GOTO actions.
    member _.LALRGoto (Nonterminal (nont, _)) {LALRState.Index = idx} =
        lalrGotoArray.[int idx].[int nont]
