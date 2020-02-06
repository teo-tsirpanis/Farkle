// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to generate Deterministic Finite Automata from `Regex`es.
/// The algorithm is a modified edition of the one found at §3.9.5 in
/// "Compilers: Principles, Techniques and Tools" by Aho, Lam, Sethi & Ullman.
module Farkle.Builder.DFABuild

open Farkle.Common
open Farkle.Collections
open Farkle.Grammar
open System
open System.Collections.Generic
open System.Collections.Immutable

[<RequireQualifiedAccess>]
type internal RegexLeaf =
    | Chars of index: int * chars: char Set
    | End of index: int * priority: int *  acceptSymbol: DFASymbol
with
    member x.Index =
        match x with
        | Chars(idx, _) | End(idx, _, _) -> idx
    member x.Characters =
        match x with
        | Chars(_, chars) -> chars
        | End _ -> Set.empty

// ALL RISE
[<Literal>]
let private TerminalPriority = 521
[<Literal>]
let private LiteralPriority = 475

type internal ConcatFirstPos = Lazy<int Set> list

type internal RegexBuildTree =
    | Concat of RegexBuild list * ConcatFirstPos
    | Alt of RegexBuild list
    | Star of RegexBuild
    | Leaf of RegexLeaf

and internal RegexBuild = {
    Tree: RegexBuildTree
    IsNullable: Lazy<bool>
    FirstPos: Lazy<int Set>
    LastPos: Lazy<int Set>
}

type internal RegexBuildLeaves = RegexBuildLeaves of ImmutableArray<RegexLeaf>
with
    member x.Length = match x with | RegexBuildLeaves arr -> arr.Length
    member x.Characters idx =
        let (RegexBuildLeaves arr) = x
        arr.[idx].Characters
    member x.AcceptData idx =
        let (RegexBuildLeaves arr) = x
        match arr.[idx] with
        | RegexLeaf.Chars _ -> None
        | RegexLeaf.End(_, priority, accSym) -> Some (accSym, priority)

let private fIsNullable =
    function
    | Concat (xs, _) -> xs |> List.forall (fun x -> x.IsNullable.Value)
    | Alt xs -> xs |> List.exists (fun x -> x.IsNullable.Value)
    | Star _ -> true
    // An empty set means the end, so I guess it is nullable.
    | Leaf x -> x.Characters.IsEmpty

let private fFirstPos =
    function
    | Concat ([], _) -> Set.empty
    | Concat (xs, _) ->
        List.fold (fun (firstPos, isNullable) x ->
            if isNullable then
                Set.union firstPos x.FirstPos.Value, x.IsNullable.Value
            else
                firstPos, false) (Set.empty, true) xs
        |> fst
    | Alt xs -> xs |> Seq.map (fun x -> x.FirstPos.Value) |> Set.unionMany
    | Star x -> x.FirstPos.Value
    | Leaf x -> Set.singleton x.Index

let private fLastPos =
    function
    | Concat ([], _) -> Set.empty
    | Concat (xs, _) ->
        List.foldBack (fun x (lastPos, isNullable) ->
            if isNullable then
                Set.union lastPos x.LastPos.Value, x.IsNullable.Value
            else
                lastPos, false) xs (Set.empty, true)
        |> fst
    | Alt xs -> xs |> Seq.map (fun x -> x.LastPos.Value) |> Set.unionMany
    | Star x -> x.LastPos.Value
    | Leaf x -> Set.singleton x.Index

let private makeLazy tree = {
    Tree = tree
    IsNullable = lazy (fIsNullable tree)
    FirstPos = lazy (fFirstPos tree)
    LastPos = lazy (fLastPos tree)
}

let internal createRegexBuild caseSensitive regexes: _ * RegexBuildLeaves =

    let createConcatFirstPos xs: ConcatFirstPos =
        (lazy Set.empty, [])
        |> List.foldBack (fun x (firstPos, xs) ->
            let firstPos = lazy(
                if x.IsNullable.Value then
                    Set.union x.FirstPos.Value firstPos.Value
                else
                    x.FirstPos.Value)
            firstPos, firstPos :: xs) xs
        |> snd

    let makeConcat x =
        match x with
        | [] -> Concat([], [])
        | _ :: rest -> Concat(x, createConcatFirstPos rest)

    let fIndex =
        let mutable i = 0
        fun () ->
            let i' = i
            i <- i + 1
            i'
    let leaves = ImmutableArray.CreateBuilder()
    let addLeaf x =
        leaves.Add(x)
        x

    let desensitivizeCase chars =
        let chars = Set.toSeq chars
        seq {
            for c in chars do
                yield Char.ToLowerInvariant(c)
                yield Char.ToUpperInvariant(c)
        } |> set

    /// Returns whether the given `Regex` contains a `Star` node.
    let rec isVariableLength =
        function
        | Regex.Concat xs
        | Regex.Alt xs -> List.exists isVariableLength xs
        | Regex.Star _ -> true
        | Regex.Chars _ -> false

    let createRegexBuildSingle regex acceptSymbol =
        let rec impl regex =
            match regex with
            | Regex.Concat xs -> xs |> List.map impl |> makeConcat
            | Regex.Alt xs -> xs |> List.map impl |> Alt
            | Regex.Star x -> x |> impl |> Star
            | Regex.Chars chars ->
                let chars =
                    if caseSensitive then
                        chars
                    else
                        desensitivizeCase chars
                RegexLeaf.Chars(fIndex(), chars) |> addLeaf |> Leaf
            |> makeLazy
        match regex with
        // If the symbol's regex's root is an Alt, we assign
        // each of its children a different priority. This
        // emulates the behavior of GOLD Parser and resolves
        // some nasty indistinguishable symbols errors.
        | Regex.Alt xs ->
            let appendEndLeaf priority =
                function
                | [] -> []
                | xs ->
                    let xs = List.map impl xs |> Alt |> makeLazy
                    let endLeaf = RegexLeaf.End(fIndex(), priority, acceptSymbol) |> addLeaf |> Leaf |> makeLazy
                    [xs; endLeaf]
                    |> makeConcat
                    |> makeLazy
                    |> List.singleton
            // There is no way that both lists below are empty.
            // Regex.Alt has always at least one child.
            let variableLengthParts, fixedLengthParts = List.partition isVariableLength xs
            appendEndLeaf TerminalPriority variableLengthParts
            @
            appendEndLeaf LiteralPriority fixedLengthParts
            // I have been careful enough to minimize the depth
            // of the regex trees, but a nested Alt does not hurt anyone.
            |> Alt
        // Otherwise, we assign a priority to the entire symbol.
        | regex ->
            let priority = if isVariableLength regex then TerminalPriority else LiteralPriority
            let endLeaf = RegexLeaf.End(fIndex(), priority, acceptSymbol) |> addLeaf |> Leaf |> makeLazy
            match regex with
            | Regex.Concat xs ->
                List.map impl xs @ [endLeaf] |> makeConcat
            | regex -> [impl regex; endLeaf] |> makeConcat
        |> makeLazy

    let theTree =
        regexes
        |> List.map (fun (regex, acceptSymbol) -> createRegexBuildSingle regex acceptSymbol)
        |> (function | [x] -> x | x -> makeLazy<| Alt x)

    theTree, leaves.ToImmutable() |> RegexBuildLeaves

let internal calculateFollowPos leafCount regex =
    let followPos = Array.replicate leafCount Set.empty
    let rec impl x =
        match x.Tree with
        | Alt xs -> List.iter impl xs
        | Concat ([], _) -> ()
        | Concat (xs, firstPoses) ->
            (xs, firstPoses) ||> List.iter2Safe (fun x firstPosOfTheRest ->
                let lastPosOfThisOne = x.LastPos.Value
                let firstPosOfTheRest = firstPosOfTheRest.Value
                lastPosOfThisOne |> Set.iter (fun idx -> followPos.[idx] <- Set.union followPos.[idx] firstPosOfTheRest))
            List.iter impl xs
        | Star x ->
            let lastPos = x.LastPos.Value
            let firstPos = x.FirstPos.Value
            lastPos |> Set.iter (fun idx -> followPos.[idx] <- Set.union followPos.[idx] firstPos)
            impl x
        | Leaf _ -> ()
    impl regex
    followPos.ToImmutableArray()

[<NoComparison; NoEquality>]
type internal DFAStateBuild = {
    Name: int Set
    Index: uint32
    Edges: SortedDictionary<char, uint32>
}
with
    static member Create name index = {Name = name; Index = index; Edges = SortedDictionary()}

let internal makeDFA prioritizeFixedLengthSymbols regex (leaves: RegexBuildLeaves) (followPos: ImmutableArray<int Set>) =
    let states = Dictionary()
    let statesList = ResizeArray()
    let unmarkedStates = Stack()
    let addNewState stateName =
        let idx = uint32 statesList.Count
        let state = DFAStateBuild.Create stateName idx
        unmarkedStates.Push(idx)
        statesList.Add(state)
        states.Add(stateName, state)
        idx

    addNewState regex.FirstPos.Value |> ignore
    while unmarkedStates.Count <> 0 do
        let S = statesList.[int <| unmarkedStates.Pop()]
        let SChars = S.Name |> Seq.map leaves.Characters |> Set.unionMany
        SChars |> Set.iter (fun a ->
            let U =
                S.Name
                |> Seq.filter (leaves.Characters >> Set.contains a)
                |> Seq.map (fun p -> followPos.[p])
                |> Set.unionMany
            let UIdx =
                if states.ContainsKey(U) then
                    states.[U].Index
                else
                    addNewState U
            S.Edges.Add(a, UIdx))
    let toDFAState state =
        let acceptSymbols =
            state.Name
            |> Seq.choose leaves.AcceptData
            // The sequence to be sorted is expected
            // to have very few elements; often none,
            // and rarely more than two.
            |> Seq.sortBy snd
            |> List.ofSeq
        let edges = RangeMap.ofSeq state.Edges
        let acceptSymbol =
            match acceptSymbols with
            // No symbols, no problem.
            | [] -> Ok None
            // If there is only one symbol, we simply take it.
            | [sym, _] -> Ok <| Some sym
            // If there are more symbols, however, we will
            // take the one with the lowest priority, if there
            // is only one, and we have allowed such prioritizing.
            // However there is a small chance that the two conflicting
            // symbols are the same, because a symbol can be derived
            // from many paths with both variable and fixed length.
            // In this case, we have to take it.
            // Remember that we had sorted the list above.
            | (sym, pri1) :: (sym2, pri2) :: _ when sym = sym2 || (pri1 < pri2 && prioritizeFixedLengthSymbols) ->
                Ok <| Some sym
            // If there are many symbols, and their priority is
            // the same, or we are not allowed to prioritize symbols,
            // we have to raise an error.
            | _ ->
                // The error should contain all symbols regardless of priority.
                acceptSymbols
                |> Seq.map fst
                |> set
                |> BuildError.IndistinguishableSymbols
                |> Error
        acceptSymbol
        |> Result.map (fun ac -> {Index = state.Index; AcceptSymbol = ac; Edges = edges})

    statesList
    |> Seq.map toDFAState
    |> collect
    |> Result.map ImmutableArray.CreateRange

/// Builds a DFA that recognizes the given `Regex`es, each
/// accepting a unique `DFASymbol`. DFA symbols that can be
/// derived by a regular expression without stars can be
/// prioritized over those with stars, in case of conflicts.
/// Moreover, the resulting DFA can be case sensitive.
let buildRegexesToDFA prioritizeFixedLengthSymbols caseSensitive regexes =
    let tree, leaves = createRegexBuild caseSensitive regexes
    let followPos = calculateFollowPos leaves.Length tree
    makeDFA prioritizeFixedLengthSymbols tree leaves followPos
