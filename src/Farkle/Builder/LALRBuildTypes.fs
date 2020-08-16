// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Builder.LALRBuildTypes

open BitCollections
open Farkle.Grammar
open System.Collections
open System.Collections.Generic
open System.Collections.Immutable

type LR0Item = {
    Production: Production
    DotPosition: int
}
with
    static member Create prod = {Production = prod; DotPosition = 0}
    member x.IsAtEnd = x.Production.Handle.Length = x.DotPosition
    member x.CurrentSymbol = x.Production.Handle.[x.DotPosition]
    member x.AdvanceDot() =
        if x.IsAtEnd then
            x
        else
            {x with DotPosition = x.DotPosition + 1}
    override x.ToString() =
        [
            yield! Seq.take x.DotPosition x.Production.Handle |> Seq.map string
            "•"
            yield! Seq.skip x.DotPosition x.Production.Handle |> Seq.map string
        ]
        |> String.concat " "
        |> sprintf "%O ::= %s" x.Production.Head

type LR0ItemSet = {
    Index: int
    Kernel: LR0Item list
    Goto: ImmutableDictionary<LALRSymbol, int>
}
with
    static member Create idx kernel = {Index = idx; Kernel = Set.toList kernel; Goto = ImmutableDictionary.Empty}

/// A data structure that holds terminal indices
/// and special flags needed for the LALR builder.
type LookaheadSet(terminalCount) =
    let ban = BitArrayNeo(terminalCount, false)
    let mutable isFrozen = false
    let mutable hasEnd = false
    let mutable hasHash = false
    let checkFrozen() =
        if isFrozen then
            invalidOp "Cannot modify a frozen collection."
    member private _.Bits = ban
    /// Whether the lookahead set contains the
    /// special EOF symbol, also known as $.
    member _.HasEnd
        with get() = hasEnd
        and set x = checkFrozen(); hasEnd <- x
    /// Whether the lookahead set contains a
    /// special symbol needed for finding the LR(1) item lookaheads.
    /// That symbol is often symbolized with a hash (#).
    member _.HasHash
        with get() = hasHash
        and set x = checkFrozen(); hasHash <- x
    /// Whether the lookahead set contains a
    /// terminal with the index of the given one.
    member _.HasTerminal
        with get(Terminal(idx, _)) = ban.[int idx]
        and set(Terminal(idx, _)) x =
            checkFrozen()
            ban.[int idx] <- x
    /// Adds a terminal to a lookahead set and returns
    /// whether the set's content was changed by this method.
    member _.Add (Terminal(termIdx, _)) =
        checkFrozen()
        ban.Set(int termIdx, true)
    /// Adds the content of another lookahead set to this one.
    /// Returns whether this lookahead set was changed by this method.
    /// Optionally, only the terminals can be added, not the special symbols.
    member _.UnionWith (laSet: LookaheadSet, ?changeExtraSymbols) =
        checkFrozen()
        if defaultArg changeExtraSymbols true then
            // Instead of "not a && b", we can write a < b.
            let changed = ban.Or laSet.Bits || hasEnd < laSet.HasEnd || hasHash < laSet.HasHash
            hasEnd <- hasEnd || laSet.HasEnd
            hasHash <- hasHash || laSet.HasHash
            changed
        else
            ban.Or laSet.Bits
    /// Creates a new, unfrozen lookahead
    /// set, identical to this one.
    member _.Clone() =
        let laSet = LookaheadSet terminalCount
        laSet.Bits.Or ban |> ignore
        laSet.HasEnd <- hasEnd
        laSet.HasHash <- hasHash
        laSet
    /// Returns an enumerator over the terminal
    /// indices that are present in this lookahead set.
    member _.GetEnumerator() = ban.GetEnumerator()
    /// Marks this lookahead set as frozen. After that,
    /// any change attempts will throw an exception.
    member _.Freeze() =
        isFrozen <- true

/// A data structure that holds the FIRST set of the nonterminals of a grammar.
type FirstSets(terminals: ImmutableArray<Terminal>, nonterminalCount) =
    let arr = Array.zeroCreate nonterminalCount
    do for i = 0 to nonterminalCount - 1 do
        arr.[i] <- LookaheadSet(terminals.Length)
    /// All the terminals of the grammar.
    /// This property exists mostly to avoid passing the terminals everywhere.
    member _.AllTerminals = terminals
    /// Adds a terminal to the FIRST set of a nonterminal.
    /// Returns whether the collection was actually changed.
    member _.Add (Nonterminal(nontIdx, _)) term =
        arr.[int nontIdx].Add term
    /// Adds the special empty symbol (also known as ε)
    /// to the FIRST set of a nonterminal.
    /// Returns whether the collection was actually changed.
    member _.AddEmpty(Nonterminal(nontIdx, _)) =
        let table = arr.[int nontIdx]
        let previousValue = table.HasEnd
        table.HasEnd <- true
        not previousValue
    /// Adds the FIRST set of the second
    /// nonterminal to the FIRST set of the first one.
    /// Returns whether the collection was actually changed.
    /// The empty symbol is not copied if it exists.
    member _.AddFromNonterminal (Nonterminal(idxDest, _)) (Nonterminal(idxSrc, _)) =
        let banDest = arr.[int idxDest]
        let banSrc = arr.[int idxSrc]
        banDest.UnionWith(banSrc, false)
    /// Adds the FIRST set of a nonterminal to a lookahead set.
    /// Returns whether the lookahead set was modified by this method.
    /// The empty symbol is not copied if it exists.
    member _.CopyToLookaheadSet (Nonterminal(idx, _)) (laSet: LookaheadSet) =
        let table = arr.[int idx]
        laSet.UnionWith(table, false)
    /// Whether the special empty symbol (also
    /// known as ε) exists for a given nonterminal.
    member _.HasEmpty
        with get(Nonterminal(idx, _)) = arr.[int idx].HasEnd
        and set(Nonterminal(idx, _)) x = arr.[int idx].HasEnd <- x
    /// Marks this collection as frozen. After that,
    /// any change attempts will throw an exception.
    member _.Freeze() =
        for laSet in arr do laSet.Freeze()

/// An LR(1) item. It's essentially an LR(0) item with a set of lookahead symbols.
type LR1Item = {
    /// The LR(0) item contained within.
    Item: LR0Item
    /// The lookahead symbols of this item.
    /// You could say that the lookahead is the set of the symbols that
    /// come after a production, but it's still too vague. Neither I do
    /// precisely know what it is. But when an item's dot is at the end
    /// the parser reduces the item's production when he encounters any
    /// of the lookahead symbols.
    Lookahead: LookaheadSet
}

/// An associative data structures with
/// arbitrary keys and values as lookahead sets.
/// Modification methods also copy special symbols.
type LookaheadSetDictionary<'key when 'key: equality>(terminalCount) =
    let dict = Dictionary<'key,LookaheadSet>()
    let mutable isFrozen = false
    let checkFrozen() =
        if isFrozen then
            invalidOp "The collection is frozen."
    /// Associates the content of the given
    /// lookahead set with the given key.
    /// Returns whether the lookahead set was modified by this method.
    member _.AddRange item lookahead =
        checkFrozen()
        match dict.TryGetValue item with
        | true, laSet -> laSet.UnionWith lookahead
        | false, _ -> dict.Add(item, lookahead.Clone()); true
    /// Returns or creates a new lookahead set associated with the given key.
    member _.GetOrCreateEmpty k =
        checkFrozen()
        match dict.TryGetValue k with
        | true, laSet -> laSet
        | false, _ ->
            let laSet = LookaheadSet(terminalCount)
            dict.Add(k, laSet)
            laSet
    /// Associates the elements that correspond to the first key
    /// with the elements that correspond to the second.
    /// Returns whether the lookahead set was modified by this method.
    member this.Union kDest kSrc =
        checkFrozen()
        match dict.TryGetValue(kSrc), dict.TryGetValue(kDest) with
        | (true, vSrcs), (true, vDests) -> vDests.UnionWith vSrcs
        | (true, vSrcs), (false, _) -> dict.Add(kDest, vSrcs.Clone()); true
        | (false, _), _ -> false
    /// Returns the lookahead set associated with the given key.
    member _.Item k = dict.[k]
    /// Tries to return the lookahead set
    /// associated with the given key, if it exists.
    member _.TryGetValue k = dict.TryGetValue k
    /// Marks this collection (and all its lookahead sets)
    /// as frozen. After that, any change attempts will throw an exception.
    member _.Freeze() =
        isFrozen <- true
        for v in dict.Values do v.Freeze()
    interface IEnumerable with
        member _.GetEnumerator() = dict.GetEnumerator() :> _
    interface IEnumerable<KeyValuePair<'key,LookaheadSet>> with
        member _.GetEnumerator() = dict.GetEnumerator() :> _

type Closure1Table = LookaheadSetDictionary<LR0Item>

type LookaheadItemsTable = LookaheadSetDictionary<LR0Item * int>
