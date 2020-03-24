// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

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
    | UInt32 of intValue: uint32
    /// [omit]
    | String of stringValue: string
    static member UInt16 (x: uint16) = UInt32 <| uint32 x
    static member Char (x: char) = UInt32 <| uint32 x

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
            Binary.BinaryPrimitives.ReverseEndianness x

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

    /// An implementation of the BinaryReader.Read7BitEncodedInt method.
    /// Adapted from .NET Core's source.
    let private read7BitEncodedUInt32 r =
        let rec impl r count shift =
            if shift = 5 * 7 then
                raise <| FormatException "Too many bytes in what should have been a 7 bit encoded UInt32."
            let b = readByte r
            let count = count ||| ((uint32 b &&& 0x7fu) <<< shift)
            if b &&& 0x80uy <> 0uy then
                impl r count (shift + 7)
            else
                count
        impl r 0u 0

    /// Reads an EGT file entry from a binary reader.
    let readEntry r =
        match readByte r with
        | 'E'B -> Entry.Empty
        | 'b'B -> r |> readByte |> Entry.Byte
        | 'B'B -> r |> readByte |> ((<>) 0uy) |> Entry.Boolean
        | 'I'B -> r |> readUInt16 |> uint32 |> Entry.UInt32
        | 'S'B -> r |> readNullTerminatedString |> Entry.String
        // These two are the EGTneo entry tags.
        // They allow more compact representation.
        | 'i'B -> r |> read7BitEncodedUInt32 |> Entry.UInt32
        // The encoding is assumed to be UTF-8.
        // Opening files from binary readers is
        // not exposed so we can be more sure.
        | 's'B -> r.ReadString() |> Entry.String
        | x -> invalidEGTf "Invalid entry code: %x." x

    /// Reads a collection of EGT file entries (record) from a binary reader.
    /// The returned buffer must be disposed to avoid memory leaks.
    /// The buffer might be bigger than the record's length, which
    /// is why the actual length is written to the given reference.
    let readRecord (entryCount: outref<_>) r =
        entryCount <-
            match readByte r with
            | 'M'B ->
                readUInt16 r |> int
            | 'm'B ->
                read7BitEncodedUInt32 r |> int
            | x -> invalidEGTf "Invalid record code, read %x" x
        let mem = MemoryPool.Shared.Rent(entryCount)
        let span = mem.Memory.Span
        for i = 0 to entryCount - 1 do
            span.[i] <- readEntry r
        mem

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

    /// Raises an error if a read-only span's
    /// length is different than the expected.
    let lengthMustBe (m: ReadOnlySpan<_>) expectedLength =
        if m.Length <> expectedLength then
            invalidEGTf "Length must have been %d but was %d" expectedLength m.Length

    /// Raises an error if a read-only span's
    /// length is less than the expected.
    let lengthMustBeAtLeast (m: ReadOnlySpan<_>) expectedLength =
        if m.Length < expectedLength then
            invalidEGTf "Length must have been at least %d but was %d" expectedLength m.Length

    // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
    // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
    // FFFFFFfFFFFFFF
    let wantEmpty (x: ReadOnlySpan<_>) idx = match x.[idx] with | Entry.Empty -> () | _ -> invalidEGTf "Invalid entry, expecting Empty."
    let wantByte (x: ReadOnlySpan<_>) idx = match x.[idx] with | Entry.Byte x -> x | _ -> invalidEGTf "Invalid entry, expecting Byte."
    let wantBoolean (x: ReadOnlySpan<_>) idx = match x.[idx] with | Entry.Boolean x -> x | _ -> invalidEGTf "Invalid entry, expecting Boolean"
    let wantUInt32 (x: ReadOnlySpan<_>) idx = match x.[idx] with | Entry.UInt32 x -> x | _ -> invalidEGTf "Invalid entry, expecting Integer."
    let wantChar x idx = wantUInt32 x idx |> char
    let wantString (x: ReadOnlySpan<_>) idx = match x.[idx] with | Entry.String x -> x | _ -> invalidEGTf "Invalid entry, expecting String"

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

    /// An implementation of the BinaryWriter.
    let rec private write7BitEncodedInt (w: BinaryWriter) x =
        if x >= 0x80u then
            w.Write(byte x ||| 0x80uy)
            write7BitEncodedInt w (x >>> 7)
        else
            w.Write(byte x)

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
        | Entry.UInt32 x ->
            w.Write('i'B)
            write7BitEncodedInt w x
        | Entry.String str ->
            w.Write('s'B)
            w.Write(str)

    /// Writes a series of `Entry`ies to a binary writer.
    let writeRecord (w: BinaryWriter) (records: ReadOnlySpan<_>) =
        w.Write('m'B)
        write7BitEncodedInt w <| uint32 records.Length
        for i = 0 to records.Length - 1 do
            writeEntry w records.[i]
