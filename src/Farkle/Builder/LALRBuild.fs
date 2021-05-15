// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to generate LALR state tables from a grammar.
module Farkle.Builder.LALRBuild

open Farkle.Grammar
open Farkle.Builder.LALRBuildTypes
open Farkle.Builder.LALRConflictResolution
open System.Collections.Generic
open System.Collections.Immutable

// Lightweight type annotations for different types of collections.
let inline private IA<'a> (x: ImmutableArray<'a>) = ignore x
let inline private RA<'a> (x: ResizeArray<'a>) = ignore x

/// Creates the LR(0) kernel sets for a grammar.
/// A function that gets the corresponding productions for
/// a nonterminal and the start symbol are required.
let private createLR0KernelItems fGetAllProductions startSymbol =

    let itemSets = ImmutableArray.CreateBuilder()
    let itemSetsToProcess = Queue()
    let kernelMap = Dictionary()

    let getOrQueueItemSet kernel =
        match kernelMap.TryGetValue(kernel) with
        | true, idx -> idx
        | false, _ ->
            kernelMap.Add(kernel, itemSets.Count)
            let itemSet = LR0ItemSet.Create itemSets.Count kernel
            itemSets.Add(itemSet)
            itemSetsToProcess.Enqueue(itemSet)
            itemSet.Index

    startSymbol
    |> fGetAllProductions
    |> Set.map LR0Item.Create
    |> getOrQueueItemSet
    |> ignore

    let q = Queue()
    // We could use a table of visited nonterminals, but it might cause
    // problems when many productions of the kernel have the same head.
    // Forget what you saw before! We must keep a table of visited items.
    // It created problems when the same production appeared in the closure,
    // but with different dot position. See the calculator's state #10 (#2 in GOLD Parser).
    let visitedItems = HashSet()
    while itemSetsToProcess.Count <> 0 do
        // Before we do anything let's clear our reused collections.
        // The queue will already be empty.
        visitedItems.Clear()

        let itemSet = itemSetsToProcess.Dequeue()
        for item in itemSet.Kernel do q.Enqueue item
        while q.Count <> 0 do
            let item = q.Dequeue()
            if visitedItems.Add item && not item.IsAtEnd then
                match item.CurrentSymbol with
                | LALRSymbol.Terminal _ -> ()
                | LALRSymbol.Nonterminal nont ->
                    for prod in fGetAllProductions nont do
                        prod |> LR0Item.Create |> q.Enqueue
        let goto =
            visitedItems
            |> Seq.filter (fun x -> not x.IsAtEnd)
            |> Seq.groupBy (fun x -> x.CurrentSymbol)
            |> Seq.map (fun (sym, kernelItems) ->
                let kernelSet = kernelItems |> Seq.map (fun x -> x.AdvanceDot()) |> set
                KeyValuePair(sym, getOrQueueItemSet kernelSet))
            |> ImmutableDictionary.CreateRange
        itemSets.[itemSet.Index] <- {itemSet with Goto = goto}

    itemSets.ToImmutable()

/// Computes the FIRST set of the `Nonterminal`s
/// of the given sequence of `Production`s.
let private computeFirstSetMap terminals nonterminals productions =
    IA<Nonterminal> nonterminals

    // The last nonterminal at the end is the starting one.
    let dict = FirstSets(terminals, nonterminals.Length + 1)
    let containsEmpty (x: LALRSymbol) =
        match x with
        | LALRSymbol.Terminal _ -> false
        | LALRSymbol.Nonterminal nont -> dict.HasEmpty nont

    for {Head = x; Handle = xs} in productions do
        if xs.IsEmpty then
            dict.AddEmpty x |> ignore

    let mutable changed = true

    while changed do
        changed <- false
        for {Head = head; Handle = handle} in productions do
            let mutable i = 0
            let len = handle.Length
            // The first member is in the FIRST set by definition.
            // Really? Who would imagine!
            while i < len && (i = 0 || containsEmpty handle.[i - 1]) do
                let changed' =
                    match handle.[i] with
                    | LALRSymbol.Terminal term ->
                        dict.Add head term
                    | LALRSymbol.Nonterminal nont ->
                        dict.AddTerminalsFromNonterminal head nont
                changed <- changed || changed'
                i <- i + 1
            if i = len - 1 && containsEmpty handle.[len - 1] then
                changed <- dict.AddEmpty head || changed

    dict.Freeze()
    dict

/// Returns the FIRST set of the given sequence of `LALRSymbol`s.
/// If all the symbols contain the empty symbol in their FIRST set,
/// the terminals in `lookahead` are included in the result.
let private getFirstSetOfSequence (firstSets: FirstSets) lookahead xs =
    let laSet = LookaheadSet(firstSets.AllTerminals.Length)
    xs
    |> Seq.fold (fun doContinue ->
        function
        | LALRSymbol.Terminal term when doContinue ->
            laSet.HasTerminal term <- true
            false
        | LALRSymbol.Nonterminal nont when doContinue ->
            firstSets.CopyTerminalsToLookaheadSet nont laSet |> ignore
            firstSets.HasEmpty nont
        | _ -> false) true
    |> function true -> laSet.UnionWith lookahead |> ignore | false -> ()
    laSet.Freeze()
    laSet

/// Computes the LR(1) CLOSURE function of a single LR(1) item, which
/// is made of the given `LR0Item` and the given set of lookahead `Terminal`s.
/// A function to get the FIRST set and the productions of a `Nonterminal` is required.
let private closure1 (fGetAllProductions: _ -> _ Set) (firstSets: FirstSets) xs =
    let q = Queue(xs: _ seq)
    let results = Closure1Table(firstSets.AllTerminals.Length)
    while q.Count <> 0 do
        let (item: LR0Item), lookahead = q.Dequeue()
        if results.AddRange item lookahead then
            if not item.IsAtEnd then
                match item.CurrentSymbol with
                | LALRSymbol.Terminal _ -> ()
                | LALRSymbol.Nonterminal nont ->
                    let first =
                        item.Production.Handle
                        |> Seq.skip (item.DotPosition + 1)
                        |> getFirstSetOfSequence firstSets lookahead
                    for prod in fGetAllProductions nont do
                        q.Enqueue(LR0Item.Create prod, first)
    results
    |> Seq.map (fun (KeyValue(k, v)) -> v.Freeze(); {Item = k; Lookahead = v})
    |> List.ofSeq

/// Computes the lookahead symbols for the given `LR0ItemSet`s.
let private computeLookaheadItems fGetAllProductions (firstSets: FirstSets) itemSets =
    IA itemSets

    let lookaheads = LookaheadItemsTable(firstSets.AllTerminals.Length)
    let propagate =
        let propagate = ResizeArray()
        let hashTerminalSet = LookaheadSet firstSets.AllTerminals.Length
        hashTerminalSet.HasHash <- true
        hashTerminalSet.Freeze()
        for itemSet in itemSets do
            for item in itemSet.Kernel do
                let closure = closure1 fGetAllProductions firstSets [item, hashTerminalSet]
                for {Item = closureItem; Lookahead = la} in closure do
                    if not closureItem.IsAtEnd then
                        match itemSet.Goto.TryGetValue(closureItem.CurrentSymbol) with
                        | true, gotoIdx ->
                            let gotoKernel = closureItem.AdvanceDot()
                            if la.HasHash then
                                propagate.Add((item, itemSet.Index), (gotoKernel, gotoIdx))
                            lookaheads.AddRange (gotoKernel, gotoIdx) la |> ignore
                        | false, _ -> ()
        propagate

    // The next line assumes the first item set's kernel contains only
    // the start production which spontaneously generates an EOF symbol by definition.
    lookaheads.GetOrCreateEmpty(itemSets.[0].Kernel.Head, 0).HasEnd <- true
    let mutable changed = true
    while changed do
        changed <- false
        for (kFrom, kTo) in propagate do
            changed <- lookaheads.Union kTo kFrom || changed
    lookaheads.Freeze()
    lookaheads

/// Creates an LALR state table.
let private createLALRStates fGetAllProductions (firstSets: FirstSets) fResolveConflict conflicts startSymbol itemSets (lookaheadTables: LookaheadItemsTable) =
    IA itemSets
    RA conflicts

    let emptyLookahead = LookaheadSet(firstSets.AllTerminals.Length)
    emptyLookahead.Freeze()
    let states = ImmutableArray.CreateBuilder itemSets.Length
    for itemSet in itemSets do
        let index = uint32 itemSet.Index
        // The book says we have to close the kernel under LR(1) to create the
        // action table. However, we have already created the GOTO table from the LR(0)
        // closure, and the only reason for the LR(1) closure is to find more lookahead
        // symbols or productions with no members. So it will be used only for the reductions.
        let closedItem =
            itemSet.Kernel
            |> Seq.map (fun kernelItem ->
                let lookahead =
                    match lookaheadTables.TryGetValue((kernelItem, int index)) with
                    | true, v -> v
                    | false, _ -> emptyLookahead
                kernelItem, lookahead)
            |> closure1 fGetAllProductions firstSets
            |> List.filter (fun x -> x.Item.IsAtEnd)
        let gotoActions =
            itemSet.Goto
            |> Seq.choose (function
                | KeyValue(LALRSymbol.Nonterminal nont, stateToGoto) ->
                    Some(KeyValuePair(nont, uint32 stateToGoto))
                | _ -> None
            )
            |> ImmutableDictionary.CreateRange
        let actions =
            let b = ImmutableDictionary.CreateBuilder()
            let mutable hasChosenNeither = false
            let addAction term action =
                match b.TryGetValue(term) with
                | _ when hasChosenNeither -> ()
                | true, existingAction ->
                    let sym = Some term
                    match fResolveConflict sym existingAction action with
                    | ChooseOption1 -> ()
                    | ChooseOption2 -> b.[term] <- action
                    | ChooseNeither ->
                        b.Remove(term) |> ignore
                        hasChosenNeither <- true
                    | CannotChoose reason ->
                        LALRConflict.Create index sym existingAction action reason
                        |> BuildError.LALRConflict
                        |> conflicts.Add
                | false, _ -> b.Add(term, action)
            for item in itemSet.Goto do
                match item with
                | KeyValue(LALRSymbol.Terminal term, stateToShiftTo) ->
                    addAction term (LALRAction.Shift(uint32 stateToShiftTo))
                | _ -> ()
            for item in closedItem do
                for idx in item.Lookahead do
                    addAction firstSets.AllTerminals.[idx] (LALRAction.Reduce item.Item.Production)
            b.ToImmutable()
        let eofAction =
            let rec resolveEOFConflict =
                function
                | [] -> None
                | [x] -> Some x
                | x1 :: x2 :: xs ->
                    match fResolveConflict None x1 x2 with
                    | ChooseOption1 -> resolveEOFConflict (x1 :: xs)
                    | ChooseOption2 -> resolveEOFConflict (x2 :: xs)
                    | ChooseNeither -> resolveEOFConflict xs
                    | CannotChoose reason ->
                        LALRConflict.Create index None x1 x2 reason
                        |> BuildError.LALRConflict
                        |> conflicts.Add
                        resolveEOFConflict xs
            closedItem
            |> Seq.choose (fun item ->
                if item.Lookahead.HasEnd then
                    Some (LALRAction.Reduce item.Item.Production)
                else
                    None)
            |> List.ofSeq
            |> resolveEOFConflict
            |> Option.map (function
                // Essentially, reducing <S'> -> <S> means accepting.
                | LALRAction.Reduce {Head = head} when head = startSymbol -> LALRAction.Accept
                | action -> action)
        states.Add {Index = index; Actions = actions; GotoActions = gotoActions; EOFAction = eofAction}
    states.MoveToImmutable()

/// Builds an LALR parsing table from the grammar with the given
/// starting symbol that contains the given `Production`s.
let buildProductionsToLALRStates (resolver: LALRConflictResolver) startSymbol terminals nonterminals productions =
    IA nonterminals
    IA productions

    let s' = Nonterminal(uint32 nonterminals.Length, "S'")
    let productions = productions.Add {
        Index = uint32 productions.Length
        Head = s'
        Handle = ImmutableArray.Create(LALRSymbol.Nonterminal startSymbol)
    }
    let nonterminalsToProductions =
        productions
        |> Seq.groupBy(fun x -> x.Head)
        |> Seq.map (fun (k, v) -> KeyValuePair(k, set v))
        |> ImmutableDictionary.CreateRange
    let fGetAllProductions k = nonterminalsToProductions.[k]

    let kernelItems = createLR0KernelItems fGetAllProductions s'
    let firstSets = computeFirstSetMap terminals nonterminals productions
    let lookaheads = computeLookaheadItems fGetAllProductions firstSets kernelItems

    let conflicts = ResizeArray()
    let resolveConflict term x1 x2 =
        match x1, x2, term with
        | LALRAction.Shift _, LALRAction.Reduce prod, Some term ->
            resolver.ResolveShiftReduceConflict term prod
        | LALRAction.Reduce prod, LALRAction.Shift _, Some term ->
            (resolver.ResolveShiftReduceConflict term prod).Invert()
        | LALRAction.Reduce prod1, LALRAction.Reduce prod2, _ ->
            resolver.ResolveReduceReduceConflict prod1 prod2
        // The reason doesn't matter; an exception will be
        // very soon thrown on an impossible conflict type.
        | _ -> CannotChoose NoPrecedenceInfo

    match createLALRStates fGetAllProductions firstSets resolveConflict conflicts s' kernelItems lookaheads with
    | theGloriousStateTable when conflicts.Count = 0 ->
        Ok theGloriousStateTable
    | _ -> conflicts |> List.ofSeq |> Error
