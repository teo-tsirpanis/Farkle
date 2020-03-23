// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open System
open System.IO
open System.Text

/// An exception that gets thrown when
/// reading an EGT file goes wrong.
type EGTFileException(msg) =
    inherit exn(msg)

/// An entry of an EGT file.
[<Struct; RequireQualifiedAccess>]
type internal Entry =
    /// [omit]
    | Empty
    /// [omit]
    | Byte of byteValue: byte
    /// [omit]
    | Boolean of boolValue: bool
    /// [omit]
    | UInt16 of intValue: uint16
    /// [omit]
    | String of stringValue: string

module internal EGTReader =

    let invalidEGT() = raise <| EGTFileException "Invalid EGT file"

    let invalidEGTf fmt = Printf.ksprintf (EGTFileException >> raise) fmt

    let inline private readByte (r: BinaryReader) = r.ReadByte()

    let private readUInt16 (r: BinaryReader) =
        let x = r.ReadUInt16()
        if BitConverter.IsLittleEndian then
            x
        else
            ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

    let readNullTerminatedString r =
        let sr = StringBuilder()
        let mutable c = readUInt16 r
        while c <> 0us do
            c |> char |> sr.Append |> ignore
            c <- readUInt16 r
        sr.ToString()

    let readEntry r =
        match readByte r with
        | 'E'B -> Entry.Empty
        | 'b'B -> r |> readByte |> Entry.Byte
        | 'B'B -> r |> readByte |> ((<>) 0uy) |> Entry.Boolean
        | 'I'B -> r |> readUInt16 |> Entry.UInt16
        | 'S'B -> r |> readNullTerminatedString |> Entry.String
        | x -> invalidEGTf "Invalid entry code: %x." x

    let readRecords fRead (r: BinaryReader) =
        // No need to get the length each time; it stays the same, and it's quite expensive, as it does some Win32 calls.
        // The stream's position on the other hand is very fast. It is just a private variable read.
        let len = r.BaseStream.Length
        let mutable arr = Array.zeroCreate 45
        while r.BaseStream.Position < len do
            match readByte r with
            | 'M'B ->
                let count = readUInt16 r |> int
                while count > arr.Length do
                    Array.Resize(&arr, arr.Length * 2)
                for i = 0 to count - 1 do
                    arr.[i] <- readEntry r
                ReadOnlyMemory(arr, 0, count) |> fRead
            | x -> invalidEGTf "Invalid record code, read %x." x

    let readEGT fHeaderCheck fRecord r =
        readNullTerminatedString r |> fHeaderCheck
        readRecords fRecord r

    let lengthMustBe (m: ReadOnlyMemory<_>) expectedLength =
        if m.Length <> expectedLength then
            invalidEGTf "Length must have been %d but was %d" expectedLength m.Length

    let lengthMustBeAtLeast (m: ReadOnlyMemory<_>) expectedLength =
        if m.Length < expectedLength then
            invalidEGTf "Length must have been at least %d but was %d" expectedLength m.Length

    // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
    // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
    // FFFFFFfFFFFFFF
    let wantEmpty (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Entry.Empty -> () | _ -> invalidEGTf "Invalid entry, expecting Empty."
    let wantByte (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Entry.Byte x -> x | _ -> invalidEGTf "Invalid entry, expecting Byte."
    let wantBoolean (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Entry.Boolean x -> x | _ -> invalidEGTf "Invalid entry, expecting Boolean"
    let wantUInt16 (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Entry.UInt16 x -> x | _ -> invalidEGTf "Invalid entry, expecting UInt16."
    let wantString (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Entry.String x -> x | _ -> invalidEGTf "Invalid entry, expecting String"

/// Functions to write EGT files.
module internal EGTWriter =

    /// Writes a null-terminated string, encoded
    /// with the UTF-16 character set to a binary writer.
    let writeNullTerminatedString str (w: BinaryWriter) =
        for i = 0 to String.length str - 1 do
            // It is documented that binary writers
            // write in little-endian format; exactly what we want.
            w.Write(uint16 str.[i])
        w.Write(0us)

    /// Writes an `Entry` to a binary writer.
    let writeEntry (e: inref<_>) (w: BinaryWriter) =
        match e with
        | Entry.Empty ->
            w.Write('E'B)
        | Entry.Byte b ->
            w.Write('b'B)
            w.Write(b)
        | Entry.Boolean b ->
            w.Write('B'B)
            w.Write(b)
        | Entry.UInt16 x ->
            w.Write('I'B)
            w.Write(x)
        | Entry.String str ->
            w.Write('S'B)
            writeNullTerminatedString str w

    /// Writes a series of `Entry`ies to a binary writer.
    let writeRecord (w: BinaryWriter) (records: ReadOnlySpan<_>) =
        w.Write('M'B)
        w.Write(uint16 records.Length)
        for i = 0 to records.Length - 1 do
            writeEntry &records.[i] w