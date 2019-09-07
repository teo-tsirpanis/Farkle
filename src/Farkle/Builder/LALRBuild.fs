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

/// A special type which represents the lookahead symbol.
type LookaheadSymbol =
    /// The lookahead symbol is a `Terminal`.
    | Terminal of Terminal
    /// The lookahead symbol is the EOF, AKA $.
    | [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>] End

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

/// Computes the lookahead symbols for the given `LR0ItemSet`s.
/// In addition to the usual dependencies, this function also
/// requires a special `Terminal` which __must not__ already exist in the grammar.
let computeLookaheadItems fGetAllProductions fGetFirstSet hashTerminal (itemSets: ImmutableArray<_>) =
    let spontaneous, propagate =
        let spontaneous = MultiMap.create()
        let propagate = MultiMap.create()
        let hashTerminalSet = Set.singleton hashTerminal
        itemSets
        |> Seq.iter (fun itemSet ->
            itemSet.Kernel
            |> Set.iter (fun kernel ->
                let closure = closure1 fGetAllProductions fGetFirstSet kernel hashTerminalSet
                closure
                |> Map.iter (fun item la ->
                    if not kernel.IsAtEnd then
                        if la.Contains(hashTerminal) then
                            propagate.Add((kernel, itemSet.Index), (item, itemSet.Goto.[kernel.CurrentSymbol])) |> ignore
                        spontaneous.AddRange((kernel, itemSet.Index), Seq.filter ((<>) hashTerminal) la) |> ignore
                )
            )
        )
        MultiMap.freeze spontaneous, MultiMap.freeze propagate

    let lookaheads = Array.init itemSets.Length (fun _ -> MultiMap.create())
    // The next line assumes the first item set's kernel contains only
    // the start production which spontaneously generates an EOF symbol by definition.
    lookaheads.[0].Add(Seq.exactlyOne itemSets.[0].Kernel, End) |> ignore
    spontaneous |> Map.iter (fun (item, idx) la -> lookaheads.[idx].AddRange(item, Set.map Terminal la) |> ignore)
    let mutable changed = true
    while changed do
        propagate
        |> Map.iter (fun (itemFrom, idxFrom) dest ->
            dest
            |> Set.iter (fun (itemTo, idxTo) ->
                changed <- lookaheads.[idxTo].UnionCross(itemTo, lookaheads.[idxFrom], itemFrom) || changed
            )
        )
    lookaheads |> Seq.map MultiMap.freeze |> ImmutableArray.CreateRange

/// <summary>Creates an LALR state table.</summary>
/// <param name="fResolveConflict">A function to pick the desired state in case of a conflict.</param>
/// <param name="fEOFResolveConflict">A function to pick the desired state in case
/// of a conflict when the parser would reach the end.</param>
/// <param name="startNonterminal">The starting nonterminal.</param>
/// <param name="itemSets">The item sets of the grammar.</param>
/// <param name="lookaheadTables">The lookahead symbols that correspond to the items of each item set.</param>
let createLALRActions fResolveConflict fEOFResolveConflict startNonterminal itemSets lookaheadTables =
    let resolveConflict actions =
        match Seq.tryExactlyOne actions with
        | Some x -> x
        | None -> fResolveConflict actions
    (itemSets, lookaheadTables)
    ||> Seq.map2 (fun itemSet lookaheadTable ->
        let index = uint32 itemSet.Index
        let gotoActions =
            itemSet.Goto
            |> Seq.choose (function
                | KeyValue(Choice2Of2 nont, stateToGoto) ->
                    Some(KeyValuePair(nont, uint32 stateToGoto))
                | _ -> None
            )
            |> ImmutableDictionary.CreateRange
        let actions =
            let shiftActions =
                itemSet.Goto
                |> Seq.choose (function
                    | KeyValue(Choice1Of2 term, stateToShiftTo) ->
                        Some(term, LALRAction.Shift(uint32 stateToShiftTo))
                    | _ -> None
                )
            let reduceActions =
                itemSet.Kernel
                |> Seq.filter (fun x -> x.IsAtEnd)
                |> Seq.collect (fun item ->
                    Map.find item lookaheadTable
                    |> Seq.choose (function Terminal term -> Some term | End -> None) 
                    |> Seq.map (fun term -> term, LALRAction.Reduce item.Production))
            let b = ImmutableDictionary.CreateBuilder()
            Seq.append shiftActions reduceActions
            |> Seq.groupBy fst
            |> Seq.map snd
            |> Seq.map resolveConflict
            |> Seq.iter b.Add
            b.ToImmutable()
        let eofAction =
            match Seq.tryExactlyOne itemSet.Kernel with
            | Some k when k.IsAtEnd && k.Production.Head = startNonterminal ->
                Some LALRAction.Accept
            | _ ->
                itemSet.Kernel
                |> Seq.filter (fun x -> x.IsAtEnd)
                |> Seq.choose (fun item ->
                    let la = Map.find item lookaheadTable
                    if Set.contains End la then
                        Some (LALRAction.Reduce item.Production)
                    else
                        None)
                |> List.ofSeq
                |> function | [] -> None | [x] -> Some x | xs -> xs |> Seq.ofList |> fEOFResolveConflict |> Some
        {Index = index; Actions = actions; GotoActions = gotoActions; EOFAction = eofAction}
    )
    |> ImmutableArray.CreateRange
    |> (fun table -> {InitialState = table.[0]; States = table})
