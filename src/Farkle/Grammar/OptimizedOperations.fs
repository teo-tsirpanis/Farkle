// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Collections
open Farkle.Grammar
open System.Collections.Immutable

/// An object that provides optimized functions for some common operations on Grammars.
/// These functions require some computationally expensive pre-processing, which is
/// performed only once, at the creation of this object.
type OptimizedOperations = {
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    GetNextDFAState: char -> DFAState -> DFAState option
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    GetLALRAction: Terminal -> LALRState -> LALRAction option
    /// Gets the next LALR state according to the given state's GOTO actions.
    LALRGoto: Nonterminal -> LALRState -> LALRState option
}

module internal OptimizedOperations =

    [<Literal>]
    /// The ASCII character with the highest code point (~).
    let ASCIIUpperBound = 127

    /// Checks if the given character belongs to ASCII.
    /// The first control characters are included.
    let isASCII c = c <= char ASCIIUpperBound

    /// Creates a two-dimensional array of DFA state indices, whose first dimension
    /// represents the index of the current DFA state, and the second represents the
    /// ASCII character that was encountered.
    let buildDFAArray (dfa: ImmutableArray<DFAState>) =
        let arr = Array2D.zeroCreate dfa.Length (ASCIIUpperBound + 1)
        let dfaOptions = dfa |> Seq.map Some |> Array.ofSeq
        dfa
        |> Seq.iteri (fun i state ->
            state.Edges.Elements
            |> Seq.iter (fun elem ->
                let state = dfaOptions.[int elem.Value]
                match isASCII elem.KeyFrom, isASCII elem.KeyTo with
                | true, true ->
                    for c = int elem.KeyFrom to int elem.KeyTo do
                        Array2D.set arr i c state
                | true, false ->
                    for c = int elem.KeyFrom to ASCIIUpperBound do
                        Array2D.set arr i c state
                | false, _ -> ()))
        arr

    /// <summary>Gets the next DFA state from the given current one, when the given character
    /// is encountered. When an ASCII character is encountered, the next state gets retrieved
    /// from an array, thus making the process much faster.</summary>
    /// <remarks>This function is intended to be curried. After the whole DFA states are passed,
    /// the array for the ASCII characters is created, which is a relatively costly procedure.</remarks>
    let getNextDFAState dfa =
        let arr = buildDFAArray dfa
        fun c (state: DFAState) ->
            if isASCII c then
                arr.[int state.Index, int c]
            else
                match RangeMap.tryFind c state.Edges with
                | ValueSome x -> Some dfa.[int x]
                | ValueNone -> None

    let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // Thanks to GOLD Parser, some cells in the array are left unused.
        // But when the time comes, the symbols of Farkle's own grammars will start at zero.
        let maxTerminalIndex = terminals |> Seq.map(fun (Terminal(idx, _)) -> idx) |> Seq.max |> int
        let arr = Array2D.zeroCreate lalr.Length (maxTerminalIndex + 1)
        lalr
        |> Seq.iter (fun {Index = stateIndex; Actions = actions} ->
            actions
            |> Seq.iter (fun (KeyValue(term, action)) -> action |> Some |> Array2D.set arr (int stateIndex) (int term.Index)))
        arr

    let getLALRAction terminals lalr =
        let arr = buildLALRActionArray terminals lalr
        fun (Terminal(terminalIndex, _)) {LALRState.Index = stateIndex} ->
            arr.[int stateIndex, int terminalIndex]

    let buildLALRGotoArray (nonterminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // Same with above.
        let maxNonterminalIndex = nonterminals |> Seq.map(fun (Nonterminal(idx, _)) -> idx) |> Seq.max |> int
        // No reason to allocate many options.
        let lalrOptions = lalr |> Seq.map Some |> Array.ofSeq
        let arr = Array2D.zeroCreate lalr.Length (maxNonterminalIndex + 1)
        lalr
        |> Seq.iter (fun {Index = stateIndex; GotoActions = actions} ->
            let stateIndex = int stateIndex
            actions
            |> Seq.iter (fun (KeyValue(nont, idx)) -> Array2D.set arr stateIndex (int nont.Index) lalrOptions.[int idx]))
        arr

    let lalrGoto nonterminals lalr =
        let arr = buildLALRGotoArray nonterminals lalr
        fun (Nonterminal(nonterminalIndex, _)) {LALRState.Index = stateIndex} ->
            arr.[int stateIndex, int nonterminalIndex]

    /// Creates an `OptimizedOperations` object that performs
    /// its operations faster, but after some pre-processing that uses more memory.
    let optimized (grammar: Grammar) = {
        GetNextDFAState = getNextDFAState grammar.DFAStates.States
        GetLALRAction = getLALRAction grammar.Symbols.Terminals grammar.LALRStates.States
        LALRGoto = lalrGoto grammar.Symbols.Nonterminals grammar.LALRStates.States
    }

    /// Creates an `OptimizedOperations` that performs
    /// its operations in the default way without any pre-processing.
    let unoptimized (grammar: Grammar) = {
        GetNextDFAState = fun c state ->
            match RangeMap.tryFind c state.Edges with
            | ValueSome idx -> Some grammar.DFAStates.[idx]
            | ValueNone -> None
        GetLALRAction = fun term state ->
            match state.Actions.TryGetValue(term) with
            | true, act -> Some act
            | false, _ -> None
        LALRGoto = fun nont state ->
            match state.GotoActions.TryGetValue(nont) with
            | true, idx -> Some grammar.LALRStates.[idx]
            | false, _ -> None
    }
