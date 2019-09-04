// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Builder.LALRBuild

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
