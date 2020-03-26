// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

open Farkle.Grammar
open System
open System.Collections.Immutable

[<AutoOpen>]
module private EGTNeoUtils =

    // I initially wanted a more fancy header, one that was readable
    // in both Base64 and ASCII, perhaps loaded with easter eggs. But
    // I settled to this, plain and boring header.
    let [<Literal>] egtNeoHeader = "Farkle Parser Tables/v6.0-alpha"

    // The headers for each section of the EGTneo file.
    // They must be present in the file in that order.

    let [<Literal>] propertiesHeader = "Properties"
    let [<Literal>] terminalsHeader = "Terminals"
    let [<Literal>] nonterminalsHeader = "Nonterminals"
    let [<Literal>] noiseSymbolsHeader = "Noise Symbols"
    let [<Literal>] startSymbolHeader = "Start Symbol"
    let [<Literal>] groupsHeader = "Groups"
    let [<Literal>] productionsHeader = "Productions"
    let [<Literal>] lalrHeader = "LALR"
    let [<Literal>] dfaHeader = "DFA"

/// Functions to read a grammar from EGTneo files.
/// EGTneo files are more compact and easier to read from Farkle.
module EGTNeoReader =

    open EGTReader
    open Farkle.Collections

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

/// Functions to write a grammar to EGTneo files.
module EGTNeoWriter =

    open EGTWriter
    open System.Buffers

    module private Implementation =

        let writeProperties w (props: ImmutableDictionary<_,_>) =
            let len = 1 + 2 * props.Count
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String propertiesHeader
            let mutable i = 1
            for p in props do
                arr.[i] <- Entry.String p.Key
                arr.[i + 1] <- Entry.String p.Value
                i <- i + 2

            writeRecord w (ReadOnlySpan arr)

        let inline writeLALRSymbols w header (symbols: ImmutableArray<_>) =
            let len = 1 + symbols.Length
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String header
            for i = 0 to symbols.Length - 1 do
                let sym = symbols.[i]
                if (^Symbol: (member Index: uint32) (sym)) <> uint32 i then
                    failwithf "%A is out of order (found at position %d)." sym i
                arr.[i + 1] <- Entry.String (^Symbol: (member Name: string) (sym))

            writeRecord w (ReadOnlySpan arr)

        let writeTerminals w (terms: ImmutableArray<Terminal>) =
            writeLALRSymbols w terminalsHeader terms

        let writeNonterminals w (terms: ImmutableArray<Nonterminal>) =
            writeLALRSymbols w nonterminalsHeader terms

        let writeNoiseSymbols w (noises: ImmutableArray<_>) =
            let len = 1 + noises.Length
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String noiseSymbolsHeader
            for i = 0 to noises.Length - 1 do
                let (Noise sym) = noises.[i]
                arr.[i + 1] <- Entry.String sym

            writeRecord w (ReadOnlySpan arr)

        let inline writeSingleValued w header entry =
            let arr = [|
                Entry.String header
                entry
            |]

            writeRecord w (ReadOnlySpan arr)

        let indexOf message (xs: ImmutableArray<_>) x =
            match xs.IndexOf x with
            | -1 -> failwithf "%s %O not found" message x
            | idx -> Entry.Int idx

        let writeStartSymbol w (nonterminals: ImmutableArray<Nonterminal>) startSymbol =
            indexOf "Start symbol" nonterminals startSymbol
            |> writeSingleValued w startSymbolHeader

        let writeResizeArray w (xs: ResizeArray<_>) =
            let count = xs.Count
            let mem = ArrayPool.Shared.Rent count
            try
                xs.CopyTo(mem)
                writeRecord w (ReadOnlySpan(mem, 0, count))
            finally
                ArrayPool.Shared.Return mem

        let writeGroups w noiseSymbols (groups: ImmutableArray<Group>) =
            let arr = ResizeArray()

            arr.Add <| Entry.String groupsHeader
            arr.Add <| Entry.Int groups.Length

            for i = 0 to groups.Length - 1 do
                let group = groups.[i]

                arr.Add <| Entry.String group.Name
                arr.Add <| Entry.Boolean group.IsTerminal
                arr.Add <|
                    match group.ContainerSymbol with
                    | Choice1Of2(Terminal(idx, _)) -> Entry.UInt32 idx
                    | Choice2Of2 x -> indexOf "Noise symbol" noiseSymbols x
                arr.Add <| match group.Start with GroupStart(name, _) -> Entry.String name
                arr.Add <|
                    match group.End with
                    | Some(GroupEnd name) -> Entry.String name
                    | None -> Entry.Empty
                arr.Add
                    (match group.AdvanceMode with
                    | AdvanceMode.Token -> 'T'B
                    | AdvanceMode.Character -> 'C'B
                    |> Entry.Byte)
                arr.Add
                    (match group.EndingMode with
                    | EndingMode.Open -> 'O'B
                    | EndingMode.Closed -> 'C'B
                    |> Entry.Byte)

                arr.Add <| Entry.Int group.Nesting.Count
                group.Nesting
                |> Seq.sort
                |> Seq.iter(Entry.UInt32 >> arr.Add)

            writeResizeArray w arr

        let writeProductions w (productions: ImmutableArray<Production>) =
            let arr = ResizeArray()

            arr.Add <| Entry.String productionsHeader
            arr.Add <| Entry.Int productions.Length

            for i = 0 to productions.Length - 1 do
                let prod = productions.[i]

                arr.Add <| Entry.Int prod.Head.Index
                arr.Add <| Entry.Int prod.Handle.Length
                prod.Handle
                |> Seq.iter (
                    function
                    | LALRSymbol.Terminal term ->
                        arr.Add <| Entry.Byte 'T'B
                        arr.Add <| Entry.Int term.Index
                    | LALRSymbol.Nonterminal nont ->
                        arr.Add <| Entry.Byte 'N'B
                        arr.Add <| Entry.Int nont.Index)

            writeResizeArray w arr

        let writeLALRAction action (arr: ResizeArray<_>) =
            match action with
            | LALRAction.Shift idx ->
                arr.Add <| Entry.Byte 'S'B
                arr.Add <| Entry.UInt32 idx
            | LALRAction.Reduce {Index = idx} ->
                arr.Add <| Entry.Byte 'R'B
                arr.Add <| Entry.UInt32 idx
            | LALRAction.Accept ->
                arr.Add <| Entry.Byte 'A'B
                arr.Add <| Entry.Empty

        let writeLALRStates w (states: ImmutableArray<LALRState>) =
            let arr = ResizeArray()

            arr.Add <| Entry.String lalrHeader
            arr.Add <| Entry.Int states.Length

            for i = 0 to states.Length - 1 do
                let s = states.[i]

                match s.EOFAction with
                | Some x -> writeLALRAction x arr
                | None ->
                    arr.Add <| Entry.Byte 0uy
                    arr.Add Entry.Empty

                arr.Add <| Entry.Int s.Actions.Count
                s.Actions
                |> Seq.iter (fun (KeyValue(term, action)) ->
                    arr.Add <| Entry.UInt32 term.Index
                    writeLALRAction action arr)

                arr.Add <| Entry.Int s.GotoActions.Count
                s.GotoActions
                |> Seq.iter (fun (KeyValue(nont, idx)) ->
                    arr.Add <| Entry.UInt32 nont.Index
                    arr.Add <| Entry.UInt32 idx)

            writeResizeArray w arr

        let writeDFAStates w noiseSymbols (groups: ImmutableArray<_>) (states: ImmutableArray<DFAState>) =
            let arr = ResizeArray()
            let writeUInt32Maybe x =
                match x with
                | Some x -> Entry.UInt32 x
                | None -> Entry.Empty
                |> arr.Add

            arr.Add <| Entry.String dfaHeader
            arr.Add <| Entry.Int states.Length

            for i = 0 to states.Length - 1 do
                let s = states.[i]

                match s.AcceptSymbol with
                | None ->
                    arr.Add <| Entry.Byte 0uy
                    arr.Add Entry.Empty
                | Some (Choice1Of4 term) ->
                    arr.Add <| Entry.Byte 'T'B
                    arr.Add <| Entry.UInt32 term.Index
                | Some (Choice2Of4 noise) ->
                    arr.Add <| Entry.Byte 'N'B
                    arr.Add <| indexOf "Noise symbol" noiseSymbols noise
                | Some (Choice3Of4 gs) ->
                    arr.Add <| Entry.Byte 'G'B
                    groups
                    |> Seq.findIndex (fun g -> g.Start = gs)
                    |> Entry.Int
                    |> arr.Add
                | Some (Choice4Of4 ge) ->
                    arr.Add <| Entry.Byte 'g'B
                    let ge = Some ge
                    groups
                    |> Seq.findIndex (fun g -> g.End = ge)
                    |> Entry.Int
                    |> arr.Add

                writeUInt32Maybe s.AnythingElse

                let elements = s.Edges.Elements
                arr.Add <| Entry.Int elements.Length
                elements
                |> Seq.iter (fun x ->
                    arr.Add <| Entry.Int x.KeyFrom
                    arr.Add <| Entry.Int x.KeyTo
                    writeUInt32Maybe x.Value)

            writeResizeArray w arr
