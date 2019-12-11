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

type internal RegexLeaf = {
    Characters: char Set
    Index: int
}

type internal RegexEnd = {
    AcceptSymbol: DFASymbol
    Index: int
}

type internal ConcatFirstPos = Lazy<int Set> list

type internal RegexBuildTree =
    | Concat of RegexBuild list * ConcatFirstPos
    | Alt of RegexBuild list
    | Star of RegexBuild
    | Chars of RegexLeaf
    | End of RegexEnd

and internal RegexBuild = {
    Tree: RegexBuildTree
    IsNullable: Lazy<bool>
    FirstPos: Lazy<int Set>
    LastPos: Lazy<int Set>
}

type internal RegexBuildLeaves = RegexBuildLeaves of ImmutableArray<obj>
with
    member x.Length = match x with | RegexBuildLeaves arr -> arr.Length
    member x.Characters idx =
        let (RegexBuildLeaves arr) = x
        match arr.[idx] with
        | :? RegexLeaf as leaf -> leaf.Characters
        | _ -> Set.empty
    member x.TryGetAcceptSymbol idx =
        let (RegexBuildLeaves arr) = x
        match arr.[idx] with
        | :? RegexEnd as leaf -> Some leaf.AcceptSymbol
        | _ -> None

let private fIsNullable =
    function
    | Concat (xs, _) -> xs |> List.forall (fun x -> x.IsNullable.Value)
    | Alt xs -> xs |> List.exists (fun x -> x.IsNullable.Value)
    | Star _ -> true
    // The set of a Chars regex must never be empty; it's just here for completeness.
    | Chars x -> x.Characters.IsEmpty
    // End is the end, so I guess it is nullable.
    | End _ -> true

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
    | Chars x -> Set.singleton x.Index
    | End x -> Set.singleton x.Index

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
    | Chars x -> Set.singleton x.Index
    | End x -> Set.singleton x.Index

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
        leaves.Add(box x)
        x

    let desensitivizeCase chars =
        let chars = Set.toSeq chars
        seq {
            for c in chars do
                yield Char.ToLowerInvariant(c)
                yield Char.ToUpperInvariant(c)
        } |> set

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
                {Characters = chars; Index = fIndex()} |> addLeaf |> Chars
            |> makeLazy
        let regexBuild = impl regex
        let endLeaf = {AcceptSymbol = acceptSymbol; Index = fIndex()} |> addLeaf |> End |> makeLazy
        match regexBuild.Tree with
        | Concat (xs, _) -> xs @ [endLeaf]
        | _ -> regexBuild :: [endLeaf]
        |> makeConcat
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
        | Chars _ | End _ -> ()
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

let internal makeDFA regex (leaves: RegexBuildLeaves) (followPos: ImmutableArray<int Set>) =
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
        let SChars = S.Name |> Seq.map leaves.Characters
        SChars |> Set.unionMany |> Set.iter (fun a ->
            let U =
                (S.Name, SChars)
                ||> Seq.zip
                |> Seq.filter (snd >> Set.contains a)
                |> Seq.map (fun (s, _) -> followPos.[s])
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
            |> Seq.choose (fun x -> leaves.TryGetAcceptSymbol(x))
            |> List.ofSeq
        let edges = RangeMap.ofSeq state.Edges
        match acceptSymbols with
        | [] -> Ok {Index = state.Index; AcceptSymbol = None; Edges = edges}
        | [sym] -> Ok {Index = state.Index; AcceptSymbol = Some sym; Edges = edges}
        | _ -> Error <| BuildError.IndistinguishableSymbols acceptSymbols
    statesList
    |> Seq.map toDFAState
    |> collect
    |> Result.map ImmutableArray.CreateRange

/// Builds a DFA that recognizes the given `Regex`es, each accepting a unique `DFASymbol`.
/// Optionally, the resulting DFA can be case sensitive.
let buildRegexesToDFA caseSensitive regexes =
    let tree, leaves = createRegexBuild caseSensitive regexes
    let followPos = calculateFollowPos leaves.Length tree
    makeDFA tree leaves followPos
