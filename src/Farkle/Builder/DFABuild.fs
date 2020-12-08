// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to generate Deterministic Finite Automata from `Regex`es.
/// The algorithm is a modified edition of the one found at §3.9.5 in
/// "Compilers: Principles, Techniques and Tools" by Aho, Lam, Sethi &amp; Ullman.
module Farkle.Builder.DFABuild

open BitCollections
open Farkle.Common
open Farkle.Collections
open Farkle.Grammar
open Farkle.Monads.Either
open System
open System.Collections.Generic
open System.Collections.Immutable

[<RequireQualifiedAccess>]
type internal RegexLeaf =
    | AllButChars of index: int * chars: char Set
    | Chars of index: int * chars: char Set
    | End of index: int * priority: int *  acceptSymbol: DFASymbol
with
    member x._IsAllButChars =
        match x with
        | AllButChars _ -> true
        | _ -> false
    member x.Index =
        match x with
        | Chars(idx, _)
        | AllButChars(idx, _)
        | End(idx, _, _) -> idx
    member x.Characters =
        match x with
        | Chars(_, chars) -> chars
        | AllButChars _
        | End _ -> Set.empty
    member x.AllButCharacters =
        match x with
        | AllButChars(_, chars) -> chars
        | Chars _
        | End _ -> Set.empty
    member x.AcceptData =
        match x with
        | Chars _
        | AllButChars _ -> None
        | End(_, priority, accSym) -> Some (accSym, priority)

// ALL RISE
[<Literal>]
let private TerminalPriority = 521
[<Literal>]
let private LiteralPriority = 475

type internal ConcatFirstPos = BitSet list

type internal RegexBuildTree =
    | Concat of RegexBuild list * ConcatFirstPos
    | Alt of RegexBuild list
    | Star of RegexBuild
    | Leaf of RegexLeaf

and internal RegexBuild = {
    Tree: RegexBuildTree
    IsNullable: bool
    FirstPos: BitSet
    LastPos: BitSet
}

type internal RegexBuildLeaves(arr: ImmutableArray<RegexLeaf>) =
    member _.Length = arr.Length
    member _.Item idx = arr.[idx]
    member _.Characters idx = arr.[idx].Characters
    member _.AllButCharacters idx = arr.[idx].AllButCharacters
    member _.AcceptData idx = arr.[idx].AcceptData

let private fIsNullable =
    function
    | Concat (xs, _) -> xs |> List.forall (fun x -> x.IsNullable)
    | Alt xs -> xs |> List.exists (fun x -> x.IsNullable)
    | Star _ -> true
    | Leaf (RegexLeaf.End _) -> true
    | Leaf _ -> false

let private fFirstPos =
    function
    | Concat ([], _) -> BitSet.Empty
    | Concat (xs, _) ->
        List.fold (fun (firstPos, isNullable) x ->
            if isNullable then
                BitSet.Union(&firstPos, &x.FirstPos), x.IsNullable
            else
                firstPos, false) (BitSet.Empty, true) xs
        |> fst
    | Alt xs -> xs |> Seq.map (fun x -> x.FirstPos) |> BitSet.UnionMany
    | Star x -> x.FirstPos
    | Leaf x -> BitSet.Singleton x.Index

let private fLastPos =
    function
    | Concat ([], _) -> BitSet.Empty
    | Concat (xs, _) ->
        List.foldBack (fun x (lastPos, isNullable) ->
            if isNullable then
                BitSet.Union(&lastPos, &x.LastPos), x.IsNullable
            else
                lastPos, false) xs (BitSet.Empty, true)
        |> fst
    | Alt xs -> xs |> Seq.map (fun x -> x.LastPos) |> BitSet.UnionMany
    | Star x -> x.LastPos
    | Leaf x -> BitSet.Singleton x.Index

let private makeRB tree = {
    Tree = tree
    IsNullable = fIsNullable tree
    FirstPos = fFirstPos tree
    LastPos = fLastPos tree
}

let internal createRegexBuild caseSensitive regexes =

    let createConcatFirstPos xs: ConcatFirstPos =
        (BitSet.Empty, [])
        |> List.foldBack (fun x (firstPos, xs) ->
            let firstPos =
                if x.IsNullable then
                    BitSet.Union(&x.FirstPos, &firstPos)
                else
                    x.FirstPos
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
        Leaf x

    let regexParseErrors = ResizeArray()

    let desensitivizeCase chars =
        if caseSensitive then
            chars
        else
            seq {
                for c in chars do
                    yield Char.ToLowerInvariant(c)
                    yield Char.ToUpperInvariant(c)
            } |> set

    /// Returns whether the given `RegexBuildTree` contains a `Star` node.
    let rec isVariableLength =
        function
        | Concat (xs, _)
        | Alt xs -> xs |> List.exists (fun x -> isVariableLength x.Tree)
        | Star _ -> true
        | Leaf _ -> false

    let createRegexBuildSingle regex acceptSymbol =
        let rec createTree regex =
            match regex with
            | Regex.Concat xs -> xs |> List.map (createTree >> makeRB) |> makeConcat
            | Regex.Alt xs -> xs |> List.map (createTree >> makeRB) |> Alt
            | Regex.Star x -> x |> createTree |> makeRB |> Star
            | Regex.Chars chars ->
                RegexLeaf.Chars(fIndex(), desensitivizeCase chars) |> addLeaf
            | Regex.AllButChars chars ->
                let chars = desensitivizeCase chars
                // It's likely that the character set would not become full until now,
                // because of the case desensitivization. Therefore we have to check it again.
                if RegexUtils.isCharSetFull chars then
                    makeConcat []
                else
                    RegexLeaf.AllButChars(fIndex(), chars) |> addLeaf
            | Regex.RegexString (_, Lazy regex) ->
                match regex with
                | Ok x -> createTree x
                | Error x ->
                    regexParseErrors.Add(BuildError.RegexParseError(acceptSymbol, x))
                    Alt []
        match createTree regex with
        // If the symbol's regex's root is an Alt, we assign
        // each of its children a different priority. This
        // emulates the behavior of GOLD Parser and resolves
        // some nasty indistinguishable symbols errors.
        | Alt xs ->
            let appendEndLeaf priority x =
                match x with
                | [] -> []
                | xs ->
                    let xs = xs |> Alt |> makeRB
                    let endLeaf =
                        RegexLeaf.End(fIndex(), priority, acceptSymbol)
                        |> addLeaf |> makeRB
                    [xs; endLeaf]
                    |> makeConcat
                    |> makeRB
                    |> List.singleton
            // There is no way that both lists below are empty.
            // Regex.Alt has always at least one child.
            let variableLengthParts, fixedLengthParts =
                xs |> List.partition (fun x -> isVariableLength x.Tree)
            appendEndLeaf TerminalPriority variableLengthParts
            @
            appendEndLeaf LiteralPriority fixedLengthParts
            |> Alt
        // Otherwise, we assign a priority to the entire symbol.
        | regex ->
            let priority = if isVariableLength regex then TerminalPriority else LiteralPriority
            let endLeaf =
                RegexLeaf.End(fIndex(), priority, acceptSymbol)
                |> addLeaf |> makeRB
            makeConcat [makeRB regex; endLeaf]
        |> makeRB

    let theTree =
        regexes
        |> List.map (fun (regex, acceptSymbol) -> createRegexBuildSingle regex acceptSymbol)
        |> (function | [x] -> x | x -> makeRB (Alt x))

    if regexParseErrors.Count = 0 then
        Ok (theTree, leaves.ToImmutable() |> RegexBuildLeaves)
    else
        regexParseErrors |> List.ofSeq |> Error

let internal calculateFollowPos leafCount regex =
    let followPos = Array.replicate leafCount BitSet.Empty
    let rec impl x =
        match x.Tree with
        | Alt xs -> for x in xs do impl x
        | Concat ([], _) -> ()
        | Concat (xs, firstPoses) ->
            (xs, firstPoses) ||> List.iter2Safe (fun x firstPosOfTheRest ->
                let lastPosOfThisOne = x.LastPos
                for idx in lastPosOfThisOne do
                    followPos.[idx] <- BitSet.Union(&followPos.[idx], &firstPosOfTheRest))
            for x in xs do impl x
        | Star x ->
            let lastPos = x.LastPos
            let firstPos = x.FirstPos
            for idx in lastPos do
                followPos.[idx] <- BitSet.Union(&followPos.[idx], &firstPos)
            impl x
        | Leaf _ -> ()
    impl regex
    followPos.ToImmutableArray()

[<NoComparison; NoEquality>]
type internal DFAStateBuild = {
    Name: BitSet
    Index: uint32
    Edges: SortedDictionary<char, uint32 option>
    mutable AnythingElse: uint32 option
}
with
    static member Create name index = {
        Name = name
        Index = index
        Edges = SortedDictionary()
        AnythingElse = None
    }

let createRangeMap xs = RangeMap.ofSeqEx xs

let internal makeDFA
    prioritizeFixedLengthSymbols regex (leaves: RegexBuildLeaves) (followPos: ImmutableArray<BitSet>) =
    let states = Dictionary()
    let statesList = ResizeArray()
    let unmarkedStates = Stack()
    let getOrAddState stateName =
        if states.ContainsKey stateName then
            states.[stateName].Index
        else
            let idx = uint32 statesList.Count
            let state = DFAStateBuild.Create stateName idx
            unmarkedStates.Push(idx)
            statesList.Add(state)
            states.Add(stateName, state)
            idx

    getOrAddState regex.FirstPos |> ignore
    while unmarkedStates.Count <> 0 do
        let S = statesList.[int <| unmarkedStates.Pop()]
        let SChars = S.Name |> Seq.map leaves.Characters |> Set.unionMany
        let SAllButChars = S.Name |> Seq.map leaves.AllButCharacters |> Set.unionMany

        for a in SAllButChars do
            S.Edges.[a] <- None
        for a in SChars do
            let U =
                S.Name
                |> Seq.filter (leaves.Characters >> Set.contains a)
                |> Seq.map (fun p -> followPos.[p])
                |> BitSet.UnionMany
            let UIdx = getOrAddState U
            // Any previous `None` set by the all-but characters will be overwritten,
            // because a concrete character takes precedence.
            // That's why we don't use `Dictionary.Add`.
            S.Edges.[a] <- Some UIdx

        S.Name
        |> Seq.filter (fun x -> leaves.[x]._IsAllButChars)
        |> Seq.map (fun p -> followPos.[p])
        |> BitSet.UnionMany
        |> (fun x ->
            if not x.IsEmpty then
                S.AnythingElse <- Some <| getOrAddState x)

    let toDFAState state =
        let acceptSymbols =
            state.Name
            |> Seq.choose leaves.AcceptData
            // The sequence to be sorted is expected
            // to have very few elements; often none,
            // and rarely more than two.
            |> Seq.sortBy snd
            |> List.ofSeq
        let edges = createRangeMap state.Edges
        let rec getAcceptSymbol xs =
            match xs with
            | [] -> Ok None
            | [sym, _] -> Ok <| Some sym
            | ((sym1, _) as first) :: (sym2, _) :: xs when sym1 = sym2 ->
                getAcceptSymbol (first :: xs)
            | (sym, pri1) :: (_, pri2) :: _ when pri1 < pri2 && prioritizeFixedLengthSymbols ->
                Ok <| Some sym
            | _ ->
                acceptSymbols
                |> Seq.map fst
                |> set
                |> BuildError.IndistinguishableSymbols
                |> Error
        getAcceptSymbol acceptSymbols
        |> Result.map (fun ac ->
            {Index = state.Index; AcceptSymbol = ac; Edges = edges; AnythingElse = state.AnythingElse})

    statesList
    |> Seq.map toDFAState
    |> Result.collect
    |> Result.map ImmutableArray.CreateRange

/// Builds a DFA that recognizes the given `Regex`es, each
/// accepting a `DFASymbol`. DFA symbols that can be
/// derived by a regular expression without stars can be
/// prioritized over those with them, in case of conflicts.
/// Moreover, the resulting DFA can be case sensitive.
let buildRegexesToDFA prioritizeFixedLengthSymbols caseSensitive regexes = either {
    let! tree, leaves = createRegexBuild caseSensitive regexes
    let followPos = calculateFollowPos leaves.Length tree
    return! makeDFA prioritizeFixedLengthSymbols tree leaves followPos
}
