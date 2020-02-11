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

[<AutoOpen>]
module private OptimizedOperations =

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

    let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        let maxTerminalIndex =
            if terminals.IsEmpty then
                0
            else
                terminals |> Seq.map(fun (Terminal(idx, _)) -> idx) |> Seq.max |> int
        let arr = Array2D.zeroCreate lalr.Length (maxTerminalIndex + 1)
        lalr
        |> Seq.iter (fun {Index = stateIndex; Actions = actions} ->
            actions
            |> Seq.iter (fun (KeyValue(term, action)) -> action |> Some |> Array2D.set arr (int stateIndex) (int term.Index)))
        arr

    let buildLALRGotoArray (nonterminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // There is no way for a grammar to lack nonterminals.
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

/// An object that provides optimized functions for some common operations on Grammars.
/// These functions require some computationally expensive pre-processing, which is
/// performed only once, at the creation of this object.
type OptimizedOperations = private {
    Grammar: Grammar
    DFAArray: DFAStateTag [,]
    LALRActionArray: LALRAction option [,]
    LALRGotoArray: LALRState option [,]
}
with
    static member Create g = {
        Grammar = g
        DFAArray = buildDFAArray g.DFAStates
        LALRActionArray = buildLALRActionArray g.Symbols.Terminals g.LALRStates
        LALRGotoArray = buildLALRGotoArray g.Symbols.Nonterminals g.LALRStates
    }
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    member x.GetNextDFAState c (state: DFAStateTag) =
        let arr = x.DFAArray
        if state.IsError then
            DFAStateTag.Error
        elif isASCII c then
            arr.[state.Value, int c]
        else
            match RangeMap.tryFind c x.Grammar.DFAStates.[state.Value].Edges with
            | ValueSome x -> DFAStateTag.FromOption x
            | ValueNone -> arr.[state.Value, ASCIIUpperBound + 1]
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    member x.GetLALRAction (Terminal (term, _)) {LALRState.Index = idx} =
        x.LALRActionArray.[int idx, int term]
    /// Gets the next LALR state according to the given state's GOTO actions.
    member x.LALRGoto (Nonterminal (nont, _)) {LALRState.Index = idx} =
        x.LALRGotoArray.[int idx, int nont]
