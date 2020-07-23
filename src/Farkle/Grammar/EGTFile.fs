// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

open System
open System.Buffers
open System.Buffers.Binary
open System.IO
open System.Text
open System.Runtime.InteropServices

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
    static member inline Int x = UInt32 <| uint32 x

[<AutoOpen>]
/// Functions to help EGT file readers.
module internal EGTReaderUtilities =

    /// Raises an exception indicating that something
    /// went wrong with reading an EGT file.
    let invalidEGT() = raise <| EGTFileException "Invalid EGT file"

    /// Like `invalidEGT`, but allows specifying a formatted message.
    let invalidEGTf fmt = Printf.ksprintf (EGTFileException >> raise) fmt

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
    let wantEmpty (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Empty -> () | _ -> invalidEGTf "Invalid entry, expecting Empty."
    let wantByte (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Byte x -> x | _ -> invalidEGTf "Invalid entry, expecting Byte."
    let wantBoolean (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Boolean x -> x | _ -> invalidEGTf "Invalid entry, expecting Boolean"
    let wantUInt32 (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.UInt32 x -> x | _ -> invalidEGTf "Invalid entry, expecting Integer."
    let wantChar x idx = wantUInt32 x idx |> char
    let wantString (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.String x -> x | _ -> invalidEGTf "Invalid entry, expecting String"

/// A class that reads EGT files from a stream.
type internal EGTReader(stream, [<Optional; DefaultParameterValue(false)>] leaveOpen) =

    let br = new BinaryReader(stream, Encoding.UTF8, leaveOpen)
    let mutable buffer = ArrayPool.Shared.Rent(128)
    let mutable length = 0

    /// Reads a null-terminated string, encoded
    /// with the UTF-16 character set from a binary reader.
    /// Commonly used to read the header of an EGT file.
    let readNullTerminatedString() =
        let sr = StringBuilder()
        let mutable c = br.ReadUInt16()
        while c <> 0us do
            sr.Append(char c) |> ignore
            c <- br.ReadUInt16()
        sr.ToString()

    /// An implementation of the BinaryReader.Read7BitEncodedInt method.
    /// Adapted from .NET Core's source.
    let read7BitEncodedUInt32() =
        let rec impl count shift =
            if shift = 5 * 7 then
                raise <| FormatException "Too many bytes in what should have been a 7 bit encoded UInt32."
            let b = br.ReadByte()
            let count = count ||| ((uint32 b &&& 0x7fu) <<< shift)
            if b &&& 0x80uy <> 0uy then
                impl count (shift + 7)
            else
                count
        impl 0u 0

    let header = readNullTerminatedString()

    /// Reads an EGT file entry from a binary reader.
    let readEntry() =
        match br.ReadByte() with
        | 'E'B -> Entry.Empty
        | 'b'B -> Entry.Byte(br.ReadByte())
        | 'B'B -> Entry.Boolean(br.ReadByte() <> 0uy)
        | 'I'B -> Entry.Int(br.ReadUInt16())
        | 'S'B -> Entry.String(readNullTerminatedString())
        // These two are the EGTneo entry tags.
        // They allow more compact representation.
        | 'i'B -> Entry.UInt32(read7BitEncodedUInt32())
        // The encoding is assumed to be UTF-8.
        // Opening files from binary readers is
        // not exposed so we can be more sure.
        | 's'B -> Entry.String(br.ReadString())
        | x -> invalidEGTf "Invalid entry code: %x." x

    let readNextRecord() =
        let entryCount =
            match br.ReadByte() with
            | 'M'B ->
                br.ReadUInt16() |> int
            | 'm'B ->
                read7BitEncodedUInt32() |> int
            | x -> invalidEGTf "Invalid record code, read %x" x
        if Array.length buffer < entryCount then
            ArrayPool.Shared.Return(buffer, true)
            buffer <- ArrayPool.Shared.Rent(max (buffer.Length * 2) entryCount)
        for i = 0 to entryCount - 1 do
            buffer.[i] <- readEntry()
        entryCount

    /// Loads the next EGT record in memory.
    /// It will then be accessible via the `Span` or `Memory` property.
    /// Note that these two properties are invalidated after this function gets called.
    member _.NextRecord() =
        if isNull buffer then
            raise (new ObjectDisposedException("buffer"))
        length <- readNextRecord()

    /// The EGT file's header. It is located at
    /// the beginning of the file and is read as
    /// soon as this object gets created.
    member _.Header = header

    /// A read-only span with the entries of the current record.
    member _.Span = ReadOnlySpan(buffer, 0, length)

    /// A read-only memory with the entries of the current record.
    member _.Memory = ReadOnlyMemory(buffer, 0, length)

    /// Whether the underlying stream has reached its end.
    /// Supported only when that stream is seekable.
    member _.IsEndOfFile = stream.Position = stream.Length

    interface IDisposable with
        member _.Dispose() =
            if not <| isNull buffer then
                ArrayPool.Shared.Return(buffer, true)
                buffer <- null
                br.Dispose()
                length <- 0

/// Functions to write EGTneo files.
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
    let private writeEntry (w: BinaryWriter) (e: inref<_>) =
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
            if isNull str then
                invalidOp "Cannot write a null string in an EGTneo file."
            w.Write('s'B)
            w.Write(str)

    /// Writes a series of `Entry`ies to a binary writer.
    let writeRecord (w: BinaryWriter) (records: ReadOnlySpan<_>) =
        w.Write('m'B)
        write7BitEncodedInt w <| uint32 records.Length
        for i = 0 to records.Length - 1 do
            writeEntry w &records.[i]

module internal EGTHeaders =

    // For better error messages only.
    let [<Literal>] CGTHeader = "GOLD Parser Tables/v1.0"

    let [<Literal>] EGTHeader = "GOLD Parser Tables/v5.0"

    // I initially wanted a more fancy header, one that was readable
    // in both Base64 and ASCII, perhaps loaded with easter eggs. But
    // I settled to this plain and boring header.
    let [<Literal>] EGTNeoHeader = "Farkle Parser Tables/v6.0"

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
