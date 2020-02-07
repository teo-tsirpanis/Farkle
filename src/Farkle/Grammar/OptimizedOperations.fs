// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Collections
open Farkle.Grammar
open System.Collections.Immutable

[<Struct>]
/// An object representing a DFA state or its absence.
/// It is returned from optimized operations.
type DFAStateTag = DFAStateTag of int
with
    /// Creates a successful `DFAStateTag`.
    static member Ok (x: uint32) = DFAStateTag <| int x
    /// A failed `DFAStateTag`.
    static member Error = DFAStateTag -1
    static member FromOption x =
        match x with
        | Some x -> DFAStateTag.Ok x
        | None -> DFAStateTag.Error
    /// The state number of this `DFAStateTag`, or
    /// a negative number.
    member x.Value = match x with DFAStateTag x -> x
    /// Whether this `DFAStateTag` represents a successful operation.
    member x.IsOk = x.Value >= 0
    /// Whether this `DFAStateTag` represents a failed operation.
    member x.IsError = x.Value < 0

/// An object that provides optimized functions for some common operations on Grammars.
/// These functions require some computationally expensive pre-processing, which is
/// performed only once, at the creation of this object.
type OptimizedOperations = {
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    GetNextDFAState: char -> DFAStateTag -> DFAStateTag
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    GetLALRAction: Terminal -> LALRState -> LALRAction option
    /// Gets the next LALR state according to the given state's GOTO actions.
    LALRGoto: Nonterminal -> LALRState -> LALRState option
}

/// Functions to create `OptimizedOperations` objects from `Grammar`s.
module OptimizedOperations =

    [<AutoOpen>]
    module Implementation =

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
            let arr = Array2D.zeroCreate dfa.Length (ASCIIUpperBound + 2)
            dfa
            |> Seq.iteri (fun i state ->
                let anythingElse = DFAStateTag.FromOption dfa.[i].AnythingElse
                for j = 0 to ASCIIUpperBound + 1 do
                    Array2D.set arr i j anythingElse
                state.Edges
                |> RangeMap.toSeq
                |> Seq.takeWhile (fun x -> isASCII x.Key)
                |> Seq.iter (fun x ->
                    Array2D.set arr i (int x.Key) (DFAStateTag.FromOption x.Value)))
            arr

        /// <summary>Gets the next DFA state from the given current one, when the given character
        /// is encountered. When an ASCII character is encountered, the next state gets retrieved
        /// from an array, thus making the process much faster.</summary>
        /// <remarks>This function is intended to be curried. After the whole DFA states are passed,
        /// the array for the ASCII characters is created, which is a relatively costly procedure.</remarks>
        let getNextDFAState dfa =
            let arr = buildDFAArray dfa
            fun c (state: DFAStateTag) ->
                if state.IsError then
                    DFAStateTag.Error
                elif isASCII c then
                    arr.[state.Value, int c]
                else
                    match RangeMap.tryFind c dfa.[state.Value].Edges with
                    | ValueSome x -> DFAStateTag.FromOption x
                    | ValueNone -> arr.[state.Value, ASCIIUpperBound + 1]

        let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
            // Thanks to GOLD Parser, some cells in the array are left unused.
            // But when the time comes, the symbols of Farkle's own grammars will start at zero.
            // Edit: the time came.
            // Also, the following line will fail if called with an empty terminal array.
            // But the caller function below protects us.
            let maxTerminalIndex = terminals |> Seq.map(fun (Terminal(idx, _)) -> idx) |> Seq.max |> int
            let arr = Array2D.zeroCreate lalr.Length (maxTerminalIndex + 1)
            lalr
            |> Seq.iter (fun {Index = stateIndex; Actions = actions} ->
                actions
                |> Seq.iter (fun (KeyValue(term, action)) -> action |> Some |> Array2D.set arr (int stateIndex) (int term.Index)))
            arr

        let getLALRAction (terminals: ImmutableArray<_>) lalr =
            // If there are no terminals, this function wouldn't even be called in the first place!
            if terminals.IsEmpty then
                fun _ _ -> None
            else
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
            // There is no way for a grammar to not have nonterminals,
            // in contrast with a gramamr without terminals.
            let arr = buildLALRGotoArray nonterminals lalr
            fun (Nonterminal(nonterminalIndex, _)) {LALRState.Index = stateIndex} ->
                arr.[int stateIndex, int nonterminalIndex]

    /// Creates an `OptimizedOperations` object that performs
    /// its operations faster, but after some pre-processing that uses more memory.
    let optimized (grammar: Grammar) = {
        GetNextDFAState = getNextDFAState grammar.DFAStates
        GetLALRAction = getLALRAction grammar.Symbols.Terminals grammar.LALRStates
        LALRGoto = lalrGoto grammar.Symbols.Nonterminals grammar.LALRStates
    }

    /// Creates an `OptimizedOperations` that performs
    /// its operations in the default way without any pre-processing.
    let unoptimized (grammar: Grammar) = {
        GetNextDFAState = fun c state ->
            let states = grammar.DFAStates
            if state.IsError then
                DFAStateTag.Error
            else
                match RangeMap.tryFind c states.[state.Value].Edges with
                | ValueSome idx -> idx
                | ValueNone -> states.[state.Value].AnythingElse
                |> DFAStateTag.FromOption
        GetLALRAction = fun term state ->
            match state.Actions.TryGetValue(term) with
            | true, act -> Some act
            | false, _ -> None
        LALRGoto = fun nont state ->
            match state.GotoActions.TryGetValue(nont) with
            | true, idx -> Some grammar.LALRStates.[int idx]
            | false, _ -> None
    }
