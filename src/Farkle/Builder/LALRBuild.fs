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
    override x.ToString() =
        [
            Seq.take x.DotPosition x.Production.Handle |> Seq.map string
            Seq.singleton "•"
            Seq.skip x.DotPosition x.Production.Handle |> Seq.map string
        ]
        |> Seq.concat
        |> String.concat " "
        |> sprintf "%O ::= %s" x.Production.Head

[<NoComparison; NoEquality>]
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
/// a nonterminal and the start symbol are required.
let createLR0KernelItems fGetAllProductions startSymbol =

    let itemSets = ImmutableArray.CreateBuilder()

    let kernelMap = Dictionary()

    let newItemSet kernel =
        kernelMap.Add(kernel, itemSets.Count)
        let itemSet = LR0ItemSet.Create itemSets.Count kernel
        itemSets.Add(itemSet)
        itemSet

    let rec impl kernel =
        let itemSet = newItemSet kernel
        let q = Queue(kernel)
        // We could use a table of visited nonterminals, but it might create problems
        // when many productions of the kernel have the same head.
        // Forget what you saw before! We must keep a table of visited items.
        // It created problems when the same production appeared in the closure,
        // but with different dot position. See the calculator's state #10 (#2 in GOLD Parser).
        let visitedItems = HashSet()
        while q.Count <> 0 do
            let item = q.Dequeue()
            if visitedItems.Add(item) then
                if not item.IsAtEnd then
                    match item.CurrentSymbol with
                    | LALRSymbol.Terminal _ -> ()
                    | LALRSymbol.Nonterminal nont ->
                        nont
                        |> fGetAllProductions
                        |> Set.iter (LR0Item.Create >> q.Enqueue)
        visitedItems
        |> Seq.filter (fun x -> not x.IsAtEnd)
        |> Seq.groupBy (fun x -> x.CurrentSymbol)
        |> Seq.iter (fun (sym, kernelItems) ->
            // All these items are guaranteed to be kernel items; their dot is never at the beginning.
            let kernelSet = kernelItems |> Seq.map (fun x -> x.AdvanceDot()) |> set
            match kernelMap.TryGetValue(kernelSet) with
            | true, idx -> itemSet.Goto.Add(sym, idx)
            | false, _ -> itemSet.Goto.Add(sym, impl kernelSet))
        itemSet.Index

    startSymbol
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
        | LALRSymbol.Terminal _ -> false
        | LALRSymbol.Nonterminal nont -> dict.Contains(nont, None)

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
                | LALRSymbol.Terminal term ->
                    dict.Add(head, Some term)
                | LALRSymbol.Nonterminal nont ->
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
        | LALRSymbol.Terminal term when doContinue -> Set.add term symbols, false
        | LALRSymbol.Nonterminal nont when doContinue ->
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
                | LALRSymbol.Terminal _ -> ()
                | LALRSymbol.Nonterminal nont ->
                    let first =
                        item.Production.Handle
                        |> Seq.skip (item.DotPosition + 1)
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
            |> Set.iter (fun item ->
                let closure = closure1 fGetAllProductions fGetFirstSet item hashTerminalSet
                closure
                |> Map.iter (fun closureItem la ->
                    if not closureItem.IsAtEnd then
                        match itemSet.Goto.TryGetValue(closureItem.CurrentSymbol) with
                        | true, gotoIdx ->
                            let gotoKernel = closureItem.AdvanceDot()
                            if la.Contains(hashTerminal) then
                                propagate.Add((item, itemSet.Index), (gotoKernel, gotoIdx)) |> ignore
                            spontaneous.AddRange((gotoKernel, gotoIdx), Seq.filter ((<>) hashTerminal) la) |> ignore
                        | false, _ -> ()
                )
            )
        )
        MultiMap.freeze spontaneous, MultiMap.freeze propagate

    let lookaheads = MultiMap.create()
    // The next line assumes the first item set's kernel contains only
    // the start production which spontaneously generates an EOF symbol by definition.
    lookaheads.Add((itemSets.[0].Kernel.MinimumElement, 0), End) |> ignore
    spontaneous |> Map.iter (fun k la -> lookaheads.AddRange(k, Seq.map Terminal la) |> ignore)
    let mutable changed = true
    while changed do
        changed <- false
        propagate
        |> Map.iter (fun kFrom dest ->
            dest
            |> Set.iter (fun kTo ->
                changed <- lookaheads.Union(kTo, kFrom) || changed
            )
        )
    MultiMap.freeze lookaheads

/// <summary>Creates an LALR state table.</summary>
/// <param name="fResolveConflict">A function to choose the preferred action in case of a conflict.</param>
/// <param name="startSymbol">The start symbol.</param>
/// <param name="itemSets">The item sets of the grammar.</param>
/// <param name="lookaheadTables">The lookahead symbols that correspond to the items of each item set.</param>
let createLALRStates fResolveConflict startSymbol itemSets (lookaheadTables: Map<_, _>) =
    let getLookahead item idx =
        match lookaheadTables.TryGetValue((item, idx)) with
        | true, v -> v
        | false, _ -> Set.empty
    itemSets
    |> Seq.map (fun itemSet ->
        let index = uint32 itemSet.Index
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
            let addAction term action =
                match b.TryGetValue(term) with
                | true, existingAction ->
                    b.[term] <- fResolveConflict index (Some term) existingAction action
                | false, _ -> b.Add(term, action)
            itemSet.Goto
            |> Seq.iter (function
                | KeyValue(LALRSymbol.Terminal term, stateToShiftTo) ->
                    addAction term (LALRAction.Shift(uint32 stateToShiftTo))
                | _ -> ()
            )
            itemSet.Kernel
            |> Seq.filter (fun x -> x.IsAtEnd)
            |> Seq.iter (fun item ->
                getLookahead item itemSet.Index
                |> Set.iter (function
                    | Terminal term -> addAction term (LALRAction.Reduce item.Production)
                    | End -> ()
                )
            )
            b.ToImmutable()
        let eofAction =
            let rec resolveEOFConflict =
                function
                | [] -> None
                | [x] -> Some x
                | x1 :: x2 :: xs -> resolveEOFConflict (fResolveConflict index None x1 x2 :: xs)
            match Seq.tryExactlyOne itemSet.Kernel with
            | Some k when k.IsAtEnd && k.Production.Head = startSymbol ->  Some LALRAction.Accept
            | _ ->
                itemSet.Kernel
                |> Seq.filter (fun x -> x.IsAtEnd)
                |> Seq.choose (fun item ->
                    let la = getLookahead item itemSet.Index
                    if Set.contains End la then
                        Some (LALRAction.Reduce item.Production)
                    else
                        None)
                |> List.ofSeq
                |> resolveEOFConflict
        {Index = index; Actions = actions; GotoActions = gotoActions; EOFAction = eofAction}
    )
    |> ImmutableArray.CreateRange
    |> (fun theBlessedLALRStates -> {InitialState = theBlessedLALRStates.[0]; States = theBlessedLALRStates})

/// Builds an LALR parsing table from the grammar that contains the given
/// `Production`s. The grammar's starting symbol and number of terminals and nonterminals are required.
let buildProductionsToLALRStates terminalCount nonterminalCount startSymbol (productions: ImmutableArray<_>) =
    let s' = Nonterminal(uint32 nonterminalCount, "S'")
    let hashTerminal = Farkle.Grammar.Terminal(uint32 terminalCount, "#")
    let productions = productions.Add {
        Index = uint32 productions.Length
        Head = s'
        Handle = ImmutableArray.Empty.Add(LALRSymbol.Nonterminal startSymbol)
    }
    let nonterminalsToProductions =
        productions
        |> Seq.groupBy(fun x -> x.Head)
        |> Seq.map (fun (k, v) -> k, set v)
        |> Map.ofSeq
    let fGetAllProductions k = Map.find k nonterminalsToProductions

    let kernelItems = createLR0KernelItems fGetAllProductions s'
    let firstSets = computeFirstSetMap productions
    let fGetFirstSet k = Map.find k firstSets
    let lookaheads = computeLookaheadItems fGetAllProductions fGetFirstSet hashTerminal kernelItems

    let conflicts = ResizeArray()
    let resolveConflict stateIndex term x1 x2 =
        LALRConflict.Create stateIndex term x1 x2 |> conflicts.Add
        x1
    match createLALRStates resolveConflict s' kernelItems lookaheads with
    | theGloriousStateTable when conflicts.Count = 0 ->
        Ok theGloriousStateTable
    | _ -> conflicts |> set |> BuildError.LALRConflict |> Error
