// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

open System
open System.Buffers
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
    let [<Literal>] lalrHeader = "LALR"
    let [<Literal>] dfaHeader = "DFA"

/// Functions to read a grammar from EGTneo files.
/// EGTneo files are more compact and easier to read from Farkle.
module EGTNeoReader =
    open EGTReader

    let checkHeader span hdr =
        let h = wantString span 0
        if h <> hdr then
            invalidEGTf "Invalid EGTneo section header: expected '%s', got '%s'." hdr h

    let readProperties span =
        lengthMustBeAtLeast span 2
        checkHeader span propertiesHeader
        let len = wantUInt32 span 1 |> int

        lengthMustBe span (2 + 2 * len)
        let b = ImmutableDictionary.CreateBuilder()
        let span = span.Slice 2
        for i = 0 to len - 1 do
            b.Add(wantString span (2 * i), wantString span (2 * i + 1))

        b.ToImmutable()

/// Functions to write a grammar to EGTneo files.
module EGTNeoWriter =
    open EGTWriter

    let writeProperties w (props: ImmutableDictionary<_,_>) =
        let len = 2 + 2 * props.Count
        use mem = MemoryPool.Shared.Rent(len)
        let span = mem.Memory.Span.Slice(0, len)

        span.[0] <- Entry.String propertiesHeader
        span.[1] <- Entry.UInt32 <| uint32 props.Count
        let mutable i = 2
        for p in props do
            span.[i] <- Entry.String p.Key
            span.[i + 1] <- Entry.String p.Value
            i <- i + 2

        writeRecord w (Span.op_Implicit span)
