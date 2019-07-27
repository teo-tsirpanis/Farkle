// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Collections
open Farkle.Grammar
open System.Collections.Immutable

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
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
        dfa
        |> Seq.iteri (fun i state ->
            state.Edges.Elements
            |> Seq.iter (fun elem ->
                match isASCII elem.KeyFrom, isASCII elem.KeyTo with
                | true, true ->
                    for c = int elem.KeyFrom to int elem.KeyTo do
                        Array2D.set arr i c (ValueSome elem.Value)
                | true, false ->
                    for c = int elem.KeyFrom to ASCIIUpperBound do
                        Array2D.set arr i c (ValueSome elem.Value)
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
                RangeMap.tryFind c state.Edges

    /// Creates a `Grammar` from its parts, and attaches to it an `OptimizedOperations` object.
    /// To ensure optimal performance, all grammars should be created from this function.
    let createOptimizedGramamr props symbols productions groups lalrStates dfaStates = {
        _Properties = props
        _Symbols = symbols
        _Productions = productions
        _Groups = groups
        _LALRStates = lalrStates
        _DFAStates = dfaStates
        OptimizedOperations = {
            _GetNextDFAState = getNextDFAState dfaStates.States
        }
    }
