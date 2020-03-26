// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to write a grammar to EGTneo files.
module Farkle.Grammar.EGTFile.EGTNeoWriter

open Farkle.Grammar
open Farkle.Grammar.EGTFile
open Farkle.Grammar.EGTFile.EGTNeoHeaders
open Farkle.Grammar.EGTFile.EGTWriter
open System
open System.Buffers
open System.Collections.Immutable

[<AutoOpen>]
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
