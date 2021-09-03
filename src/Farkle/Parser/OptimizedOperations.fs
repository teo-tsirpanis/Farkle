// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Collections
open Farkle.Grammar
open System
open System.Collections.Immutable
open System.Diagnostics
open System.Runtime.CompilerServices
open System.Text

[<Struct; IsReadOnly>]
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

[<Struct; IsReadOnly>]
/// An efficient representation of a DFA.
// It stores each of the edges' items (starting and ending character,
// as well as the state to go to) to a separate array, increasing compactness
// and locality, at the expense of non-constant time and random memory access.
// Speaking of random memory access, it mostly happens at the edgeEnds array;
// other arrays are accessed only once or twice per operation.
// TODO: do the same with the LALR states. It was not prioritized; the DFA is slower.
type private OptimizedDFA private(stateStarts: int ImmutableArray, edgeStarts: char ImmutableArray, edgeEnds: char ImmutableArray, edgeTransitions: DFAStateTag ImmutableArray, anythingElse: DFAStateTag ImmutableArray) =

    // Adapted from .NET's binary search function.
    static let rec binarySearch lo hi k (xs: char ImmutableArray) =
        if lo <= hi then
            let median = int ((uint32 hi + uint32 lo) >>> 1)
            match xs.[median].CompareTo k with
            | 0 -> median
            | x when x < 0 -> binarySearch (median + 1) hi k xs
            | _ -> binarySearch lo (median - 1) k xs
        else
            ~~~ lo

    static member Create (grammar: Grammar) =
        let dfaStates = grammar.DFAStates
        let totalEdges =
            dfaStates
            |> Seq.sumBy (fun x -> x.Edges.Elements.Length)
        let stateStarts = ImmutableArray.CreateBuilder dfaStates.Length
        let edgeStarts = ImmutableArray.CreateBuilder totalEdges
        let edgeEnds = ImmutableArray.CreateBuilder totalEdges
        let edgeTransitions = ImmutableArray.CreateBuilder totalEdges
        let anythingElse = ImmutableArray.CreateBuilder dfaStates.Length

        for state in dfaStates.AsSpan() do
            stateStarts.Add edgeStarts.Count
            state.AnythingElse |> DFAStateTag.FromOption |> anythingElse.Add
            let edges = state.Edges.Elements
            for edge in edges do
                edgeStarts.Add edge.KeyFrom
                edgeEnds.Add edge.KeyTo
                edge.Value |> DFAStateTag.FromOption |> edgeTransitions.Add

        OptimizedDFA(stateStarts.MoveToImmutable(), edgeStarts.MoveToImmutable(), edgeEnds.MoveToImmutable(), edgeTransitions.MoveToImmutable(), anythingElse.MoveToImmutable())

    member _.GetNextDFAState c state =
        let lo = stateStarts.[state]
        let hi =
            if state < stateStarts.Length - 1 then
                stateStarts.[state + 1] - 1
            else
                edgeStarts.Length - 1
        if lo <= hi then
            let idx =
                match binarySearch lo hi c edgeEnds with
                | x when x >= 0 -> x
                | x -> Math.Min(~~~x, hi)
            if edgeStarts.[idx] <= c && c <= edgeEnds.[idx] then
                edgeTransitions.[idx]
            else
                anythingElse.[state]
        else
            anythingElse.[state]

[<Struct>]
/// Describes which characters to search for to find the
/// next decision point of a character group. A decision
/// point is a place in the input text where either a
/// group can end or another nestable group can start.
type private CharacterGroupSearchAction = {
    /// The characters to search for.
    SearchCharacters: string
    /// If set to true, the tokenizer will have to search
    /// for any of these characters in the input text. Otherwise
    /// it will have to search for all of them in order.
    DoIndexOfAny: bool
}

[<AutoOpen>]
module private OptimizedOperations =

    let private createJaggedArray2D length1 length2 =
        let arr = Array.zeroCreate length1
        for i = 0 to length1 - 1 do
            arr.[i] <- Array.zeroCreate length2
        arr

    let buildCharacterGroupSearchActionArray (groups: ImmutableArray<Group>) =
        groups
        |> Seq.map (fun group ->
            let struct (characters, doIndexOfAny) =
                if group.AdvanceMode = AdvanceMode.Token then
                    // These actions only make sense in character groups. We will
                    // skip the entire process and return a dummy action on token
                    // groups (that are currently not possible from Farkle).
                    null, false
                elif group.Nesting.IsEmpty then
                    match group.End with
                    // If the group ends with a literal and cannot be nested we can
                    // match the entire literal instead of only its first character.
                    | Some (GroupEnd ge) -> ge, false
                    | None -> "\r\n", true
                else
                    let sb = StringBuilder()
                    let mutable foundNewLine = false
                    match group.End with
                    | Some (GroupEnd ge) -> sb.Append ge.[0] |> ignore
                    | None ->
                        if not foundNewLine then
                            foundNewLine <- true
                            sb.Append "\r\n" |> ignore
                    for groupIdx in group.Nesting do
                        let (GroupStart(gs, _)) = groups.[int groupIdx].Start
                        sb.Append gs.[0] |> ignore
                    sb.ToString(), true
            {SearchCharacters = characters; DoIndexOfAny = doIndexOfAny})
        |> Array.ofSeq

    let buildLALRActionArray (terminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        let arr = createJaggedArray2D lalr.Length terminals.Length
        for {Index = stateIndex; Actions = actions} in lalr do
            for KeyValue(Terminal(idx, _), action) in actions do
                arr.[int stateIndex].[int idx] <- Some action
        arr

    let buildLALRGotoArray (nonterminals: ImmutableArray<_>) (lalr: ImmutableArray<_>) =
        // No reason to allocate many options.
        let lalrOptions = lalr |> Seq.map Some |> Array.ofSeq
        let arr = createJaggedArray2D lalr.Length nonterminals.Length
        for {Index = stateIndex; GotoActions = gotoActions} in lalr do
            let stateIndex = int stateIndex
            for KeyValue(Nonterminal(nontIdx, _), idx) in gotoActions do
                arr.[stateIndex].[int nontIdx] <- lalrOptions.[int idx]
        arr

/// An object that provides optimized functions for some common operations on Grammars.
/// These functions require some memory-intensive
/// pre-processing, which is performed only once per grammar.
[<Sealed>]
type internal OptimizedOperations private(grammar: Grammar) =
    // I love this type. It surprisingly simply allows
    // for some very efficient decoupled object caches.
    // It's also fast and thread-safe. Getting a value
    // from a CWT does not lock unless the key does not exist.
    static let cache = ConditionalWeakTable()
    static let cacheItemCreator = ConditionalWeakTable.CreateValueCallback OptimizedOperations

    let optimizedDfa = OptimizedDFA.Create grammar
    let groupSearchActionArray = buildCharacterGroupSearchActionArray grammar.Groups
    let lalrActionArray = buildLALRActionArray grammar.Symbols.Terminals grammar.LALRStates
    let lalrGotoArray = buildLALRGotoArray grammar.Symbols.Nonterminals grammar.LALRStates
    /// Returns an `OptimizedOperations` object associated
    /// with the given `Grammar`. Multiple invocations of
    /// this method with the same grammar might return the
    /// same instance which is collected with the grammar.
    static member Create grammar = cache.GetValue(grammar, cacheItemCreator)
    /// Gets the next DFA state from the given current one, when the given character is encountered.
    member _.GetNextDFAState c state = optimizedDfa.GetNextDFAState c state
    /// Returns the index of the next input character that might either end
    /// the group the tokenizer is in or enter another one that can be nested.
    /// If such character does not exist the method returns -1.
    member _.IndexOfCharacterGroupDecisionPoint (inputCharacters: ReadOnlySpan<char>, groupIdx) =
        Debug.Assert(grammar.Groups.[groupIdx].AdvanceMode = AdvanceMode.Character,
            "IndexOfCharacterGroupDecisionPoint was called for a token group.")
        let action = groupSearchActionArray.[groupIdx]
        let searchCharacters = action.SearchCharacters.AsSpan()
        if action.DoIndexOfAny then
            inputCharacters.IndexOfAny(searchCharacters)
        else
            inputCharacters.IndexOf(searchCharacters)
    /// Gets the LALR action from the given state that corresponds to the given terminal.
    member _.GetLALRAction (Terminal (term, _)) {LALRState.Index = idx} =
        lalrActionArray.[int idx].[int term]
    /// Gets the next LALR state according to the given state's GOTO actions.
    member _.LALRGoto (Nonterminal (nont, _)) {LALRState.Index = idx} =
        lalrGotoArray.[int idx].[int nont]
