// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Builder.LALRBuild

open Farkle.Collections
open Farkle.Grammar
open System.Collections.Generic
open System.Collections.Immutable

type LR0Item = {
    Production: Production
    DotPosition: int
}
with
    static member Create prod = {Production = prod; DotPosition = 0}
    member x.IsKernel startNonterminal =
        x.Production.Head = startNonterminal
        // What about the items with empty productions? Are they kernel items as well?
        || x.DotPosition <> 0
    member x.IsAtEnd = x.Production.Handle.Length = x.DotPosition
    member x.CurrentSymbol = x.Production.Handle.[x.DotPosition]
    member x.AdvanceDot() =
        if x.IsAtEnd then
            x
        else
            {x with DotPosition = x.DotPosition + 1}

type LR0ItemSet = {
    Index: int
    Kernel: Set<LR0Item>
    Goto: Dictionary<LALRSymbol, int>
}
with
    static member Create idx kernel = {Index = idx; Kernel = kernel; Goto = Dictionary()}

let getAllProductions (map: Map<Nonterminal, Production Set>) x = map.[x]

/// Creates the LR(0) kernel sets for a grammar.
/// A function that gets the corresponding productions for
/// a nonterminal and the starting nonterminal are required.
let createLR0KernelItems fGetAllProductions startNonterminal =

    let itemSets = ImmutableArray.CreateBuilder()

    let kernelMap = Dictionary()

    let newItemSet kernel =
        kernelMap.Add(kernel, itemSets.Count)
        let itemSet = LR0ItemSet.Create itemSets.Count kernel
        itemSets.Add(itemSet)
        itemSet

    let rec impl kernel =
        let itemSet = newItemSet kernel
        let q = Queue()
        // We could use a table of visited nonterminals, but it might create problems
        // when many productions of the kernel have the same head.
        let visitedProductions = HashSet()
        let allItems = ResizeArray()
        Set.iter q.Enqueue kernel
        while q.Count <> 0 do
            let item = q.Dequeue()
            if not <| visitedProductions.Contains(item.Production) then
                visitedProductions.Add(item.Production) |> ignore
                allItems.Add(item)
                if not item.IsAtEnd then
                    match item.CurrentSymbol with
                    | Choice1Of2 _ -> ()
                    | Choice2Of2 nont ->
                        nont
                        |> fGetAllProductions
                        |> Set.iter (LR0Item.Create >> q.Enqueue)
        allItems
        |> Seq.filter (fun x -> not x.IsAtEnd)
        |> Seq.groupBy (fun x -> x.CurrentSymbol)
        |> Seq.iter (fun (sym, kernelItems) ->
            // All these items are guaranteed to be kernel items; their dot is never at the beginning.
            let kernelSet = kernelItems |> Seq.map (fun x -> x.AdvanceDot()) |> set
            match kernelMap.TryGetValue(kernelSet) with
            | true, idx -> itemSet.Goto.Add(sym, idx)
            | false, _ -> itemSet.Goto.Add(sym, impl kernelSet))
        itemSet.Index

    startNonterminal
    |> fGetAllProductions
    |> Set.map LR0Item.Create
    |> impl
    |> ignore

    itemSets.ToImmutable()

/// Computes the FIRST set of the `Nonterminal`s of the given sequence of `Production`s.
/// A `None` in the set of a nonterminal is the empty symbol, AKA Epsilon, or ε.
let computeFirstSetMap productions =
    let dict = MultiMap.create()
    let containsEmpty (x: LALRSymbol) =
        match x with
        | Choice1Of2 _ -> false
        | Choice2Of2 nont -> dict.Contains(nont, None)

    productions
    |> Seq.iter (fun {Head = x; Handle = xs} ->
        if xs.IsEmpty then
            dict.Add(x, None) |> ignore)

    let mutable changed = true

    while changed do
        changed <- false
        productions
        |> Seq.iter (fun {Head = head; Handle = handle} ->
            let mutable i = 0
            let len = handle.Length
            // The first member is in the FIRST set by definition.
            // Really? Who would imagine!
            while i < len && (i = 0 || containsEmpty handle.[i - 1]) do
                match handle.[i] with
                | Choice1Of2 term ->
                    dict.Add(head, Some term)
                | Choice2Of2 nont ->
                    dict.Union(head, nont)
                |> (fun x -> changed <- changed || x)
                i <- i + 1)

    MultiMap.freeze dict

/// Returns the FIRST set of the given sequence of `LALRSymbol`s.
/// If all the symbols contain the empty symbol in their FIRST set,
/// the terminals in `lookahead` are included in the result.
/// A function to get the FIRST set of each `Nonterminal` is required.
let getFirstSetOfSequence fGetFirstSet lookahead (xs: LALRSymbol seq) =
    xs
    |> Seq.fold (fun (symbols, doContinue) ->
        function
        | Choice1Of2 term when doContinue -> Set.add term symbols, false
        | Choice2Of2 nont when doContinue ->
            let first = fGetFirstSet nont
            let firstClean = first |> Seq.choose id |> set
            Set.union symbols firstClean, Set.contains None first
        | _ -> symbols, false) (Set.empty, true)
    |> (fun (first, containsEmpty) -> if containsEmpty then Set.union first lookahead else first)

/// Computes the LR(1) CLOSURE function of a single LR(1) item, which
/// is made of the given `LR0Item` and the given set of lookahead `Terminal`s.
/// A function to get the FIRST set and the productons of a `Nonterminal` is required.
let closure1 fGetAllProductions fGetFirstSet item lookahead =
    let q = Queue()
    let results = MultiMap.create()
    q.Enqueue(item, lookahead)
    while q.Count <> 0 do
        let (item: LR0Item), lookahead = q.Dequeue()
        if results.AddRange(item, lookahead) then
            if not item.IsAtEnd then
                match item.CurrentSymbol with
                | Choice1Of2 _ -> ()
                | Choice2Of2 nont ->
                    let first =
                        item.Production.Handle
                        |> Seq.skip item.DotPosition
                        |> getFirstSetOfSequence fGetFirstSet lookahead
                    nont
                    |> fGetAllProductions
                    |> Set.iter (fun prod -> q.Enqueue(LR0Item.Create prod, first))
    MultiMap.freeze results
