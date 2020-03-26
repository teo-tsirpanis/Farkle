// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to write a grammar to EGTneo files.
module internal Farkle.Grammar.EGTFile.EGTNeoWriter

open Farkle.Grammar
open Farkle.Grammar.EGTFile
open Farkle.Grammar.EGTFile.EGTHeaders
open Farkle.Grammar.EGTFile.EGTWriter
open System
open System.Buffers
open System.Collections.Immutable

[<AutoOpen>]
module private Implementation =
    
    type IndexMap = ImmutableDictionary<uint32, uint32>

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
        let dict = ImmutableDictionary.CreateBuilder()

        arr.[0] <- Entry.String header
        for i = 0 to symbols.Length - 1 do
            let sym = symbols.[i]
            dict.[(^Symbol: (member Index: uint32) (sym))] <- uint32 i
            arr.[i + 1] <- Entry.String (^Symbol: (member Name: string) (sym))

        writeRecord w (ReadOnlySpan arr)
        dict.ToImmutable()

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
        | idx -> uint32 idx

    let writeStartSymbol w (nonterminalMap: IndexMap) (startSymbol: Nonterminal) =
        nonterminalMap.[startSymbol.Index]
        |> Entry.UInt32
        |> writeSingleValued w startSymbolHeader

    let writeResizeArray w (xs: ResizeArray<_>) =
        let count = xs.Count
        let mem = ArrayPool.Shared.Rent count
        try
            xs.CopyTo(mem)
            writeRecord w (ReadOnlySpan(mem, 0, count))
        finally
            ArrayPool.Shared.Return mem

    let writeGroups w noiseSymbols (terminalMap: IndexMap) (groups: ImmutableArray<Group>) =
        let arr = ResizeArray()

        arr.Add <| Entry.String groupsHeader
        arr.Add <| Entry.Int groups.Length

        for i = 0 to groups.Length - 1 do
            let group = groups.[i]

            arr.Add <| Entry.String group.Name
            arr.Add <| Entry.Boolean group.IsTerminal
            arr.Add <|
                (match group.ContainerSymbol with
                | Choice1Of2(Terminal(idx, _)) -> terminalMap.[idx]
                | Choice2Of2 x -> indexOf "Noise symbol" noiseSymbols x
                |> Entry.UInt32)
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

    let writeProductions w (terminalMap: IndexMap) (nonterminalMap: IndexMap)
        (productions: ImmutableArray<Production>) =
        let arr = ResizeArray()

        arr.Add <| Entry.String productionsHeader
        arr.Add <| Entry.Int productions.Length

        for i = 0 to productions.Length - 1 do
            let prod = productions.[i]

            arr.Add <| Entry.Int nonterminalMap.[prod.Head.Index]
            arr.Add <| Entry.Int prod.Handle.Length
            prod.Handle
            |> Seq.iter (
                function
                | LALRSymbol.Terminal term ->
                    arr.Add <| Entry.Byte 'T'B
                    arr.Add <| Entry.Int terminalMap.[term.Index]
                | LALRSymbol.Nonterminal nont ->
                    arr.Add <| Entry.Byte 'N'B
                    arr.Add <| Entry.Int nonterminalMap.[nont.Index])

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

    let writeLALRStates w (terminalMap: IndexMap) (nonterminalMap: IndexMap) (states: ImmutableArray<LALRState>) =
        let arr = ResizeArray()

        arr.Add <| Entry.String lalrHeader
        arr.Add <| Entry.Int states.Length

        for i = 0 to states.Length - 1 do
            let s = states.[i]

            match s.EOFAction with
            | Some x -> writeLALRAction x arr
            | None ->
                arr.Add Entry.Empty
                arr.Add Entry.Empty

            arr.Add <| Entry.Int s.Actions.Count
            s.Actions
            |> Seq.iter (fun (KeyValue(term, action)) ->
                arr.Add <| Entry.UInt32 terminalMap.[term.Index]
                writeLALRAction action arr)

            arr.Add <| Entry.Int s.GotoActions.Count
            s.GotoActions
            |> Seq.iter (fun (KeyValue(nont, idx)) ->
                arr.Add <| Entry.UInt32 nonterminalMap.[nont.Index]
                arr.Add <| Entry.UInt32 idx)

        writeResizeArray w arr

    let writeDFAStates w (terminalMap: IndexMap) noiseSymbols (groups: ImmutableArray<_>) (states: ImmutableArray<DFAState>) =
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
                arr.Add Entry.Empty
                arr.Add Entry.Empty
            | Some (Choice1Of4 term) ->
                arr.Add <| Entry.Byte 'T'B
                arr.Add <| Entry.UInt32 terminalMap.[term.Index]
            | Some (Choice2Of4 noise) ->
                arr.Add <| Entry.Byte 'N'B
                indexOf "Noise symbol" noiseSymbols noise |> Entry.UInt32 |> arr.Add
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

let write w (grammar: Grammar) =
    // For symmetry with the reader, the header
    // will be written at the EGT module.
    writeProperties w grammar.Properties
    // In GOLD Parser's EGT files, the symbols do
    // not start from zero; we have to adjust them.
    let terminalMap = writeTerminals w grammar.Symbols.Terminals
    let nonterminalMap = writeNonterminals w grammar.Symbols.Nonterminals
    writeNoiseSymbols w grammar.Symbols.NoiseSymbols
    writeStartSymbol w nonterminalMap grammar.StartSymbol
    writeGroups w grammar.Symbols.NoiseSymbols terminalMap grammar.Groups
    writeProductions w terminalMap nonterminalMap grammar.Productions
    writeLALRStates w terminalMap nonterminalMap grammar.LALRStates
    writeDFAStates w terminalMap grammar.Symbols.NoiseSymbols grammar.Groups grammar.DFAStates
