// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Builder.LALRBuildTypes

open BitCollections
open Farkle.Grammar
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
    Kernel: Set<LR0Item>
    Goto: ImmutableDictionary<LALRSymbol, int>
}
with
    static member Create idx kernel = {Index = idx; Kernel = kernel; Goto = ImmutableDictionary.Empty}

type LookaheadSet(terminalCount) =
    let ban = BitArrayNeo(terminalCount, false)
    let mutable isFrozen = false
    let mutable hasEnd = false
    let mutable hasHash = false
    let checkFrozen() =
        if isFrozen then
            invalidOp "The collection is frozen."
    member private _.Bits = ban
    member _.HasEnd
        with get() = hasEnd
        and set x = checkFrozen(); hasEnd <- x
    member _.HasHash
        with get() = hasHash
        and set x = checkFrozen(); hasHash <- x
    member _.HasTerminal
        with get(Terminal(idx, _)) = ban.[int idx]
        and set(Terminal(idx, _)) x =
            checkFrozen()
            ban.[int idx] <- x
    member _.Add (Terminal(termIdx, _)) =
        checkFrozen()
        ban.Set(int termIdx, true)
    member _.AddRange (laSet: LookaheadSet, ?changeExtraSymbols) =
        checkFrozen()
        if defaultArg changeExtraSymbols true then
            let changed = ban.Or laSet.Bits || hasEnd <> laSet.HasEnd || hasHash <> laSet.HasHash
            hasEnd <- laSet.HasEnd
            hasHash <- laSet.HasHash
            changed
        else
            ban.Or laSet.Bits
    member _.GetEnumerator() = ban.GetEnumerator()
    member _.Freeze() =
        isFrozen <- true

type FirstSets(terminals: ImmutableArray<Terminal>, nonterminalCount) =
    let arr = Array.zeroCreate nonterminalCount
    do for i = 0 to nonterminalCount - 1 do
        arr.[i] <- LookaheadSet(terminals.Length)
    member _.AllTerminals = terminals
    member _.Add (Nonterminal(nontIdx, _)) term =
        arr.[int nontIdx].Add term
    member _.AddEmpty(Nonterminal(nontIdx, _)) =
        let table = arr.[int nontIdx]
        let previousValue = table.HasEnd
        table.HasEnd <- true
        not previousValue
    member _.AddFromNonterminal (Nonterminal(idxDest, _)) (Nonterminal(idxSrc, _)) =
        let banDest = arr.[int idxDest]
        let banSrc = arr.[int idxSrc]
        banDest.AddRange(banSrc, false)
    member _.CopyToLookaheadSet (Nonterminal(idx, _)) (laSet: LookaheadSet) =
        let table = arr.[int idx]
        laSet.AddRange(table, false)
    member _.HasEmpty
        with get(Nonterminal(idx, _)) = arr.[int idx].HasEnd
        and set(Nonterminal(idx, _)) x = arr.[int idx].HasEnd <- x
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

type LookaheadSetDictionary<'key when 'key: equality>(terminalCount) =
    let dict = Dictionary<'key,LookaheadSet>()
    member _.AddRange item lookahead =
        match dict.TryGetValue item with
        | true, laSet -> laSet.AddRange lookahead
        | false, _ -> dict.Add(item, lookahead); true
    member _.GetOrCreateEmpty k =
        match dict.TryGetValue k with
        | true, laSet -> laSet
        | false, _ ->
            let laSet = LookaheadSet(terminalCount)
            dict.Add(k, laSet)
            laSet
    member this.Union kDest kSrc =
        let src = this.GetOrCreateEmpty kSrc
        let dest = this.GetOrCreateEmpty kDest
        dest.AddRange src
    member _.Item k = dict.[k]
    member _.TryGetValue k = dict.TryGetValue k
    member _.AsEnumerable() = Seq.readonly dict
    member _.GetEnumerator() = dict.GetEnumerator()

type Closure1Table = LookaheadSetDictionary<LR0Item>

type LookaheadItemsTable = LookaheadSetDictionary<LR0Item * int>

/// A special type which represents the lookahead symbol.
type LookaheadSymbol =
    /// The lookahead symbol is a `Terminal`.
    | Terminal of Terminal
    /// The lookahead symbol is a special symbol used for the LR(1) closure.
    | Hash
    /// The lookahead symbol is the EOF, AKA $.
    | End
