// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open System
open System.Buffers
open System.IO
open System.Text

/// An exception that gets thrown when
/// reading an EGT file goes wrong.
type EGTFileException(msg) = inherit exn(msg)

/// An entry of an EGT file.
[<Struct; RequireQualifiedAccess>]
type Entry =
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

/// Functions to read EGT files.
module internal EGTReader =

    /// Raises an exception indicating that something
    /// went wrong with reading an EGT file.
    let invalidEGT() = raise <| EGTFileException "Invalid EGT file"

    /// Like `invalidEGT`, but allows specifying a formatted message.
    let invalidEGTf fmt = Printf.ksprintf (EGTFileException >> raise) fmt

    let inline private readByte (r: BinaryReader) = r.ReadByte()

    let private readUInt16 (r: BinaryReader) =
        let x = r.ReadUInt16()
        if BitConverter.IsLittleEndian then
            x
        else
            ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

    /// Reads a null-terminated string, encoded
    /// with the UTF-16 character set from a binary reader.
    /// Commonly used to read the header of an EGT file.
    let readNullTerminatedString r =
        let sr = StringBuilder()
        let mutable c = readUInt16 r
        while c <> 0us do
            c |> char |> sr.Append |> ignore
            c <- readUInt16 r
        sr.ToString()

    /// Reads an EGT file entry from a binary reader.
    let readEntry r =
        match readByte r with
        | 'E'B -> Entry.Empty
        | 'b'B -> r |> readByte |> Entry.Byte
        | 'B'B -> r |> readByte |> ((<>) 0uy) |> Entry.Boolean
        | 'I'B -> r |> readUInt16 |> Entry.UInt16
        | 'S'B -> r |> readNullTerminatedString |> Entry.String
        | x -> invalidEGTf "Invalid entry code: %x." x

    /// Reads a collection of EGT file entries (record) from a binary reader.
    /// The returned buffer must be disposed to avoid memory leaks.
    /// The buffer might be bigger than the record's length, which
    /// is why the actual length is written to the given reference.
    let readRecord (entryCount: outref<_>) r =
        match readByte r with
        | 'M'B ->
            entryCount <- readUInt16 r |> int
            let mem = MemoryPool.Shared.Rent(entryCount)
            let span = mem.Memory.Span
            for i = 0 to entryCount - 1 do
                span.[i] <- readEntry r
            mem
        | x -> invalidEGTf "Invalid record code, read %x." x

    /// Reads all EGT file records from a binary reader
    /// and passes them to the given function, until the reader ends.
    let readRecords fRead (r: BinaryReader) =
        let len = r.BaseStream.Length
        let mutable entryCount = 0
        while r.BaseStream.Position < len do
            use mem = readRecord &entryCount r
            mem.Memory.Slice(0, entryCount)
            |> Memory.op_Implicit
            |> fRead

    /// Like `readRecords`, but adds a function to
    /// check the file's header for errors.
    let readEGT fHeaderCheck fRecord r =
        readNullTerminatedString r |> fHeaderCheck
        readRecords fRecord r

    /// Raises an error if a read-only memory's
    /// length is different than the expected.
    let lengthMustBe (m: ReadOnlyMemory<_>) expectedLength =
        if m.Length <> expectedLength then
            invalidEGTf "Length must have been %d but was %d" expectedLength m.Length

    /// Raises an error if a read-only memory's
    /// length is less than the expected.
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
        // God, why does String.length null return zero?
        if isNull str then
            nullArg "str"
        for i = 0 to String.length str - 1 do
            // It is documented that binary writers
            // write in little-endian format; exactly what we want.
            w.Write(uint16 str.[i])
        w.Write(0us)

    /// Writes an `Entry` to a binary writer.
    let writeEntry (w: BinaryWriter) e =
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
            writeEntry w records.[i]
