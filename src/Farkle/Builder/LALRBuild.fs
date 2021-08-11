// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to generate LALR state tables from a grammar.
module Farkle.Builder.LALRBuild

open System
open Farkle.Grammar
open Farkle.Builder.LALRBuildTypes
open Farkle.Builder.LALRConflictResolution
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading

// Lightweight type annotations for different types of collections.
let inline private IA<'a> (x: ImmutableArray<'a>) = ignore x
let inline private RA<'a> (x: ResizeArray<'a>) = ignore x

/// Creates the LR(0) kernel sets for a grammar.
/// A function that gets the corresponding productions for
/// a nonterminal and the start symbol are required.
let private createLR0KernelItems (ct: CancellationToken) fGetAllProductions startSymbol =

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
    |> Seq.map LR0Item.Create
    |> set
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
        ct.ThrowIfCancellationRequested()
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
let private computeFirstSetMap (ct: CancellationToken) terminals nonterminals productions =
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
        ct.ThrowIfCancellationRequested()
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
let private getFirstSetOfSequence (firstSets: FirstSets) lookahead (xs: ReadOnlySpan<_>) =
    let laSet = LookaheadSet(firstSets.AllTerminals.Length)
    let mutable enumerator = xs.GetEnumerator()
    let mutable doContinue = true
    while enumerator.MoveNext() && doContinue do
        match enumerator.Current with
        | LALRSymbol.Terminal term ->
            laSet.HasTerminal term <- true
            doContinue <- false
        | LALRSymbol.Nonterminal nont ->
            firstSets.CopyTerminalsToLookaheadSet nont laSet |> ignore
            doContinue <- firstSets.HasEmpty nont
    if doContinue then
        laSet.UnionWith lookahead |> ignore
    laSet.Freeze()
    laSet

/// Computes the LR(1) CLOSURE function of a single LR(1) item, which
/// is made of the given `LR0Item` and the given set of lookahead `Terminal`s.
/// A function to get the FIRST set and the productions of a `Nonterminal` is required.
let private closure1 (fGetAllProductions: _ -> _ list) (firstSets: FirstSets) xs =
    let q = Queue(xs: _ seq)
    let results = Closure1Table(firstSets.AllTerminals.Length)
    while q.Count <> 0 do
        let (item: LR0Item), lookahead = q.Dequeue()
        if results.AddRange item lookahead then
            if not item.IsAtEnd then
                match item.CurrentSymbol with
                | LALRSymbol.Terminal _ -> ()
                | LALRSymbol.Nonterminal nont ->
                    let handle = item.Production.Handle.AsSpan().Slice(item.DotPosition + 1)
                    let first = getFirstSetOfSequence firstSets lookahead handle
                    for prod in fGetAllProductions nont do
                        q.Enqueue(LR0Item.Create prod, first)
    results
    |> Seq.map (fun (KeyValue(k, v)) -> v.Freeze(); {Item = k; Lookahead = v})
    |> List.ofSeq

/// Computes the lookahead symbols for the given `LR0ItemSet`s.
let private computeLookaheadItems (ct: CancellationToken) fGetAllProductions (firstSets: FirstSets) itemSets =
    IA itemSets

    let lookaheads = LookaheadItemsTable(firstSets.AllTerminals.Length)
    let propagate =
        let propagate = ResizeArray()
        let hashTerminalSet = LookaheadSet firstSets.AllTerminals.Length
        hashTerminalSet.HasHash <- true
        hashTerminalSet.Freeze()
        for itemSet in itemSets do
            for item in itemSet.Kernel do
                ct.ThrowIfCancellationRequested()
                let closure = closure1 fGetAllProductions firstSets [item, hashTerminalSet]
                for {Item = closureItem; Lookahead = la} in closure do
                    if not closureItem.IsAtEnd then
                        match itemSet.Goto.TryGetValue(closureItem.CurrentSymbol) with
                        | true, gotoIdx ->
                            let gotoKernel = closureItem.AdvanceDot()
                            if la.HasHash then
                                propagate.Add struct(struct(item, itemSet.Index), struct(gotoKernel, gotoIdx))
                            lookaheads.AddRange (gotoKernel, gotoIdx) la |> ignore
                        | false, _ -> ()
        propagate

    // The next line assumes the first item set's kernel contains only
    // the start production which spontaneously generates an EOF symbol by definition.
    lookaheads.GetOrCreateEmpty(itemSets.[0].Kernel.Head, 0).HasEnd <- true
    let mutable changed = true
    while changed do
        changed <- false
        for kFrom, kTo in propagate do
            changed <- lookaheads.Union kTo kFrom || changed
    lookaheads.Freeze()
    lookaheads

/// Creates an LALR state table.
let private createLALRStates (ct: CancellationToken) fGetAllProductions (firstSets: FirstSets) fResolveConflict errors startSymbol itemSets (lookaheadTables: LookaheadItemsTable) =
    IA itemSets
    RA errors

    let emptyLookahead = LookaheadSet(firstSets.AllTerminals.Length)
    emptyLookahead.Freeze()
    let states = ImmutableArray.CreateBuilder itemSets.Length
    let statesConflicted = ImmutableArray.CreateBuilder itemSets.Length
    for itemSet in itemSets do
        ct.ThrowIfCancellationRequested()
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
        let struct(actions, actionsConflicted) =
            let b = ImmutableDictionary.CreateBuilder()
            let bConflicted = ImmutableDictionary.CreateBuilder()
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
                        |> errors.Add
                | false, _ -> b.Add(term, action)
            let addActionConflicted k v =
                match bConflicted.TryGetValue(k) with
                // Yes, we append a list, but it will happen very
                // rarely; only on a conflict, and that list will
                // even more rarely have more than 1-2 elements.
                | true, vs -> bConflicted.[k] <- vs @ [v]
                | false, _ -> bConflicted.[k] <- [v]
            for item in itemSet.Goto do
                match item with
                | KeyValue(LALRSymbol.Terminal term, stateToShiftTo) ->
                    let action = LALRAction.Shift(uint32 stateToShiftTo)
                    addAction term action
                    addActionConflicted term action
                | _ -> ()
            for item in closedItem do
                for idx in item.Lookahead do
                    let term = firstSets.AllTerminals.[idx]
                    let action = LALRAction.Reduce item.Item.Production
                    addAction term action
                    addActionConflicted term action
            b.ToImmutable(), bConflicted.ToImmutable()
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
                    |> errors.Add
                    resolveEOFConflict xs
        let eofActions =
            closedItem
            |> Seq.choose (fun item ->
                if item.Lookahead.HasEnd then
                    Some (LALRAction.Reduce item.Item.Production)
                else
                    None)
            |> List.ofSeq
        let resolvedEofAction =
            eofActions
            |> resolveEOFConflict
            |> Option.map (function
                // Essentially, reducing <S'> -> <S> means accepting.
                | LALRAction.Reduce {Head = head} when head = startSymbol -> LALRAction.Accept
                | action -> action)
        states.Add {Index = index; Actions = actions; GotoActions = gotoActions; EOFAction = resolvedEofAction}
        statesConflicted.Add {Index = index; Actions = actionsConflicted; GotoActions = gotoActions; EOFActions = eofActions}

    if errors.Count <> 0 then
        statesConflicted.MoveToImmutable()
        |> BuildError.LALRConflictReport
        |> errors.Add
    states.MoveToImmutable()

/// <summary>Builds an LALR parsing table.</summary>
/// <param name="ct">Used to cancel the operation.</param>
/// <param name="options">Used to further configure the operation.</param>
/// <param name="resolver">Used to resolve LALR conflicts.</param>
/// <param name="startSymbol">The grammar's starting symbol.</param>
/// <param name="terminals">The grammar's terminals.</param>
/// <param name="nonterminals">The grammar's nonterminals.</param>
/// <param name="productions">The grammar's productions.</param>
/// <returns>An immutable array of the LALR states or a
/// list of the errors that were encountered.</returns>
/// <exception cref="OperationCanceledException"><paramref name="ct"/> was triggered.</exception>
let buildProductionsToLALRStatesEx ct (options: BuildOptions)
    (resolver: LALRConflictResolver) startSymbol terminals nonterminals productions =
    ignore options
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
        |> Seq.map (fun (k, v) -> KeyValuePair(k, List.ofSeq v))
        |> ImmutableDictionary.CreateRange
    let fGetAllProductions k = nonterminalsToProductions.[k]

    let kernelItems = createLR0KernelItems ct fGetAllProductions s'
    let firstSets = computeFirstSetMap ct terminals nonterminals productions
    let lookaheads = computeLookaheadItems ct fGetAllProductions firstSets kernelItems

    let errors = ResizeArray()
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

    match createLALRStates ct fGetAllProductions firstSets resolveConflict errors s' kernelItems lookaheads with
    | theGloriousStateTable when errors.Count = 0 ->
        Ok theGloriousStateTable
    | _ -> errors |> List.ofSeq |> Error

/// <summary>Builds an LALR parsing table.</summary>
/// <param name="resolver">Used to resolve LALR conflicts.</param>
/// <param name="startSymbol">The grammar's starting symbol.</param>
/// <param name="terminals">The grammar's terminals.</param>
/// <param name="nonterminals">The grammar's nonterminals.</param>
/// <param name="productions">The grammar's productions.</param>
/// <returns>An immutable array of the LALR states or a
/// list of the errors that were encountered.</returns>
let buildProductionsToLALRStates resolver startSymbol terminals nonterminals productions =
    buildProductionsToLALRStatesEx
        CancellationToken.None BuildOptions.Default resolver
        startSymbol terminals nonterminals productions
