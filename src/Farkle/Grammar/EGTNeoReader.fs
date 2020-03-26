// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to read a grammar from EGTneo files.
/// EGTneo files are more compact and easier to read from Farkle.
module Farkle.Gramamr.EGTFile.EGTNeoReader

open Farkle.Grammar
open Farkle.Grammar.EGTFile
open Farkle.Grammar.EGTFile.EGTNeoHeaders
open Farkle.Grammar.EGTFile.EGTReader
open Farkle.Collections
open System
open System.Collections.Immutable

[<AutoOpen>]
module private Implementation =

    let checkHeader span hdr =
        lengthMustBeAtLeast span 1
        let h = wantString span 0
        if h <> hdr then
            span.Slice 1
        else
            invalidEGTf "Invalid EGTneo section header: expected '%s', got '%s'." hdr h
            ReadOnlySpan.Empty

    let readProperties span =
        let span = checkHeader span propertiesHeader
        let len = span.Length / 2

        let b = ImmutableDictionary.CreateBuilder()
        for i = 0 to len - 1 do
            b.Add(wantString span (2 * i), wantString span (2 * i + 1))

        b.ToImmutable()

    // Terminals, nonterminals and noise symbols are stored in
    // about the same format. The latter just ignore the index.
    let inline readLALRSymbols header fSymbol span =
        let span = checkHeader span header

        let b = ImmutableArray.CreateBuilder(span.Length)
        for i = 0 to span.Length - 1 do
            b.Add(fSymbol(uint32 i, wantString span i))

        b.MoveToImmutable()

    let readTerminals span = readLALRSymbols terminalsHeader Terminal span

    let readNonterminals span = readLALRSymbols terminalsHeader Nonterminal span

    let readNoiseSymbols span = readLALRSymbols noiseSymbolsHeader (snd >> Noise) span

    let inline readSingleValued header span =
        let span = checkHeader span header
        lengthMustBe span 1
        &span.[0]

    let readStartSymbol (nonterminals: ImmutableArray<Nonterminal>) span =
        match readSingleValued startSymbolHeader span with
        | Entry.UInt32 idx when int idx < nonterminals.Length ->
            nonterminals.[int idx]
        | x -> invalidEGTf "Cannot retrieve start symbol. Got %A." x

    let readGroups (terminals: ImmutableArray<_>) (noiseSymbols: ImmutableArray<_>) span =
        let span = checkHeader span groupsHeader
        let groupCount = wantUInt32 span 0 |> int
        let groups = ImmutableArray.CreateBuilder(groupCount)

        let mutable i = 1
        while i < span.Length do
            let name = wantString span (i + 0)
            let container =
                let containerIndex = int <| wantUInt32 span (i + 2)
                if wantBoolean span (i + 1) then
                    Choice1Of2 terminals.[containerIndex]
                else
                    Choice2Of2 noiseSymbols.[containerIndex]
            let start = GroupStart(wantString span (i + 3), uint32 groups.Count)
            let gEnd =
                match span.[i + 4] with
                | Entry.Empty -> None
                | Entry.String str -> Some <| GroupEnd str
                | e -> invalidEGTf "Cannot retrieve group end for %s. Got %A." name e
            let advanceMode =
                match wantByte span (i + 5) with
                | 'T'B -> AdvanceMode.Token
                | 'C'B -> AdvanceMode.Character
                | x -> invalidEGTf "Cannot retrieve group advance mode for %s. Got %x." name x
            let endingMode =
                match wantByte span (i + 6) with
                | 'O'B -> EndingMode.Open
                | 'C'B -> EndingMode.Closed
                | x -> invalidEGTf "Cannot retrieve group ending mode for %s. Got %x." name x
            let nestingCount = wantUInt32 span (i + 7) |> int
            i <- i + 8

            let nesting =
                let nesting = ImmutableHashSet.CreateBuilder()
                let span = span.Slice(i)
                for i = 0 to span.Length - 1 do
                    nesting.Add(wantUInt32 span i) |> ignore
                nesting.ToImmutable()

            groups.Add {
                Name = name
                ContainerSymbol = container
                Start = start
                End = gEnd
                AdvanceMode = advanceMode
                EndingMode = endingMode
                Nesting = nesting
            }
            i <- i + nestingCount

        groups.MoveToImmutable()

    let readProductions (terminals: ImmutableArray<_>) (nonterminals: ImmutableArray<_>) span =
        let span = checkHeader span productionsHeader
        let prodCount = wantUInt32 span 0 |> int
        let prods = ImmutableArray.CreateBuilder(prodCount)

        let mutable i = 1
        while i < span.Length do
            let head = nonterminals.[int <| wantUInt32 span (i + 0)]

            let handleLength = int <| wantUInt32 span (i + 1)
            i <- i + 2

            let handle =
                let handle = ImmutableArray.CreateBuilder(handleLength)
                let span = span.Slice(i)
                for i = 0 to handleLength - 1 do
                    let idx = wantUInt32 span (2 * i + 1) |> int
                    match wantByte span (2 * i + 0) with
                    | 'T'B -> LALRSymbol.Terminal terminals.[idx]
                    | 'N'B -> LALRSymbol.Nonterminal nonterminals.[idx]
                    | x -> invalidEGTf "Cannot retrieve production handle tag. Got %x" x
                    |> handle.Add
                handle.MoveToImmutable()

            prods.Add {
                Index = uint32 prods.Count
                Head = head
                Handle = handle
            }
            i <- i + 2 * handleLength

        prods.MoveToImmutable()

    let readLALRAction (productions: ImmutableArray<_>) span idx =
        match wantByte span idx, span.[idx + 1] with
        | 'S'B, Entry.UInt32 x -> LALRAction.Shift x
        | 'R'B, Entry.UInt32 x -> LALRAction.Reduce productions.[int x]
        | 'A'B, _ -> LALRAction.Accept
        | x, e -> invalidEGTf "Invalid LALR action entries. Got %x and %A" x e

    let readLALRStates (terminals: ImmutableArray<_>) (nonterminals: ImmutableArray<_>) productions span =
        let span = checkHeader span lalrHeader
        let stateCount = wantUInt32 span 0 |> int
        let states = ImmutableArray.CreateBuilder(stateCount)

        let mutable i = 1
        while i < span.Length do
            let eofAction =
                match span.[i + 0] with
                | Entry.Empty -> None
                | _ -> Some <| readLALRAction productions span (i + 1)

            let actionCount = wantUInt32 span (i + 3) |> int
            i <- i + 4

            let actions =
                let actions = ImmutableDictionary.CreateBuilder()
                let span = span.Slice i
                for i = 0 to actionCount - 1 do
                    let term = terminals.[wantUInt32 span (3 * i + 0) |> int]
                    let action = readLALRAction productions span (3 * i + 1)
                    actions.Add(term, action)
                actions.ToImmutable()
            i <- i + 3 * actionCount

            let gotoCount = wantUInt32 span i |> int
            i <- i + 1

            let goto =
                let goto = ImmutableDictionary.CreateBuilder()
                let span = span.Slice i
                for i = 0 to gotoCount - 1 do
                    let nont = nonterminals.[wantUInt32 span (2 * i + 0) |> int]
                    let idx = wantUInt32 span (2 * i + 1)
                    goto.Add(nont, idx)
                goto.ToImmutable()
            i <- i + 2 * gotoCount

            states.Add {
                Index = uint32 states.Count
                Actions = actions
                EOFAction = eofAction
                GotoActions = goto
            }

        states.MoveToImmutable()

    let readUInt32Maybe (span: ReadOnlySpan<_>) idx =
        match span.[idx] with
        | Entry.Empty -> None
        | Entry.UInt32 x -> Some x
        | e -> invalidEGTf "Invalid state index. Expected Empty or UInt32, got %A" e

    let readDFAStates (terminals: ImmutableArray<_>) (noiseSymbols: ImmutableArray<_>)
        (groups: ImmutableArray<_>) span =
        let span = checkHeader span dfaHeader
        let stateCount = wantUInt32 span 0 |> int
        let states = ImmutableArray.CreateBuilder(stateCount)

        let mutable i = 1
        while i < span.Length do
            let acceptSymbol =
                match span.[i + 0] with
                | Entry.Empty -> None
                | Entry.Byte x ->
                    let idx = wantUInt32 span (i + 1) |> int
                    match x with
                    | 'T'B -> terminals.[idx] |> Choice1Of4
                    | 'N'B -> noiseSymbols.[idx] |> Choice2Of4
                    | 'G'B -> groups.[idx].Start |> Choice3Of4
                    | 'g'B ->
                        match groups.[idx].End with
                        | Some ge -> Choice4Of4 ge
                        | None -> invalidEGT()
                    | x -> invalidEGTf "Invalid DFA accept symbol tag. Got %x" x
                    |> Some
                | x -> invalidEGTf "Invalid DFA accept symbol entry. Expected Empty or Byte, got %A" x

            let anythingElse = readUInt32Maybe span (i + 2)
            let edgeCount = wantUInt32 span (i + 3) |> int
            i <- i + 4

            let edges =
                let edges = ResizeArray(edgeCount)
                let span = span.Slice i
                for i = 0 to edgeCount - 1 do
                    let cFrom = wantChar span (3 * i + 0)
                    let cTo = wantChar span (3 * i + 1)
                    let destination = readUInt32Maybe span (3 * i + 2)
                    edges.Add(Seq.singleton (cFrom, cTo), destination)
                match RangeMap.ofRanges (edges.ToArray()) with
                | Some edges -> edges
                | None -> invalidEGTf "Invalid DFA state range map."

            states.Add {
                Index = uint32 states.Count
                Edges = edges
                AcceptSymbol = acceptSymbol
                AnythingElse = anythingElse
            }

        states.MoveToImmutable()

let read r =
    let mutable buffer = Array.zeroCreate 128
    let mutable len = 0
    let readNext() = len <- readRecord r &buffer

    readNext()
    let properties = readProperties (ReadOnlySpan(buffer, 0, len))
    readNext()
    let terminals = readTerminals (ReadOnlySpan(buffer, 0, len))
    readNext()
    let nonterminals = readNonterminals (ReadOnlySpan(buffer, 0, len))
    readNext()
    let noiseSymbols = readNoiseSymbols (ReadOnlySpan(buffer, 0, len))
    readNext()
    let startSymbol = readStartSymbol nonterminals (ReadOnlySpan(buffer, 0, len))
    readNext()
    let groups = readGroups terminals noiseSymbols (ReadOnlySpan(buffer, 0, len))
    readNext()
    let productions = readProductions terminals nonterminals (ReadOnlySpan(buffer, 0, len))
    readNext()
    let lalrStates = readLALRStates terminals nonterminals productions (ReadOnlySpan(buffer, 0, len))
    readNext()
    let dfaStates = readDFAStates terminals noiseSymbols groups (ReadOnlySpan(buffer, 0, len))

    let symbols = {
        Terminals = terminals
        Nonterminals = nonterminals
        NoiseSymbols = noiseSymbols
    }

    {
        _Properties = properties
        _StartSymbol = startSymbol
        _Symbols = symbols
        _Productions = productions
        _Groups = groups
        _LALRStates = lalrStates
        _DFAStates = dfaStates
    }
