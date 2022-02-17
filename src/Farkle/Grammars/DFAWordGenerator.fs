// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammars

open Farkle.Collections
open System
open System.Collections.Generic
open System.Collections.Immutable

module internal DFAWordGenerator =

    // Enumerates the first and last characters of gaps in a DFA state.
    // A gap is an interval of unassigned characters.
    let private enumerateGaps (state: DFAState) = seq {
        let edges = state.Edges.ElementsArray
        let mutable gapStart = Char.MinValue
        let mutable hasLastCharacter = false
        for i = 0 to edges.Length - 1 do
            let currentEdge = edges.[i]
            if currentEdge.KeyFrom <> gapStart then
                // We could yield tuples but we don't need to, and
                // a flat character sequence makes things simpler.
                yield gapStart
                yield uint16 currentEdge.KeyFrom - 1us |> char
            gapStart <- uint16 currentEdge.KeyTo + 1us |> char
            hasLastCharacter <- currentEdge.KeyTo = Char.MaxValue
        if not hasLastCharacter then
            yield gapStart
            yield Char.MaxValue
    }

    // Tries to find an unassigned character on a DFA state.
    let private getUnassignedChar state =
        if not <| state.Edges.ContainsKey 'a' then
            'a'
        else
            let gaps = enumerateGaps state |> List.ofSeq
            gaps
            |> List.tryFind (fun c ->
                not (Char.IsControl c || Char.IsWhiteSpace c || Char.IsSeparator c || Char.IsSurrogate c))
            |> function
            | Some c -> c
            | None ->
                // We can't fail this one; Anything Else edges are removed on complete states.
                List.head gaps
            |> fun c ->
                if state.Edges.ContainsKey c then
                    failwith "Could not find a character to cross an Anything Else edge. Please report it on GitHub."
                c

    // Uses a breadth-first search algorithm to calculate for each DFA
    // state, the state that goes back to the initial one with the least
    // amount of steps, and a character that triggers this transition.
    // Its implementation assumes that DFAs start from zero,
    // and that all their states are reachable. If any of these
    // assumptions is ever broken, this function must be revisited.
    let private createPredecessors (states: DFAState ImmutableArray) =
        let q = Queue states.Length
        let predecessors = Array.create states.Length struct(-1, '\000')
        let isUnvisited idx = match predecessors.[idx] with x, _ -> x = -1
        let visit c currentState nextState =
            let nextState = int nextState
            if isUnvisited nextState then
                predecessors.[nextState] <- currentState, c
                q.Enqueue nextState
        q.Enqueue 0
        predecessors.[0] <- 0, '\000'
        while q.Count <> 0 do
            let currentState = q.Dequeue()
            let state = states.[currentState]
            // We just pick the starting character of the edge's interval, nothing complicated here.
            // The words will have little variety, trying to give them more while also being
            // deterministic is non-trivial and not important enough to try.
            for {KeyFrom = cFrom; Value = v} in state.Edges do
                match v with
                | Some nextState -> visit cFrom currentState nextState
                | None -> ()
            match state.AnythingElse with
            // getUnassignedChar is expensive and we avoid to call it if we know it won't matter.
            | Some ae when isUnvisited (int ae) ->
                visit (getUnassignedChar states.[currentState]) currentState ae
            | _ -> ()
        predecessors

    // Generates a sequence of characters that lead from the initial DFA state to the given one.
    // The first argument of this function is intended to be partially applied.
    let generateWordLeadingToState states =
        let predecessors = createPredecessors states
        fun stateIndex ->
            if stateIndex = 0 then
                ""
            else
                // We build the string backwards, which means that we can't use a string builder.
                // Instead we use an array and reverse it at the end. We know that the string can't
                // be longer than the number of states, so we can use a fixed-size array.
                let sb = Array.zeroCreate states.Length
                let mutable charsWritten = 0
                let mutable i = stateIndex
                while i <> 0 do
                    let struct(nextState, c) = predecessors.[i]
                    sb.[charsWritten] <- c
                    charsWritten <- charsWritten + 1
                    i <- nextState
                let span = Span(sb, 0, charsWritten)
                span.Reverse()
                span.ToString()
