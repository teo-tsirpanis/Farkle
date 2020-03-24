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

        let readStartSymbol (nonterminals: ImmutableArray<Nonterminal>) span =
            let span = checkHeader span startSymbolHeader
            lengthMustBe span 1
            let idx = wantUInt32 span 0 |> int
            if idx < nonterminals.Length then
                nonterminals.[idx]
            else
                invalidEGTf "Start symbol index out of range (#%d out of %d nonterminals)." idx nonterminals.Length

/// Functions to write a grammar to EGTneo files.
module EGTNeoWriter =

    open EGTWriter

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

        let writeStartSymbol w (nonterminals: ImmutableArray<Nonterminal>) startSymbol =
            let len = 1 + 1
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String noiseSymbolsHeader
            match nonterminals.IndexOf startSymbol with
            | -1 -> failwithf "Start symbol %O not found" startSymbol
            | idx -> arr.[1] <- Entry.UInt32 <| uint32 idx

            writeRecord w (ReadOnlySpan arr)
