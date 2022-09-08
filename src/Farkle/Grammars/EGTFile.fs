// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammars.EGTFile

open System
open System.Buffers
open System.IO
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// An exception that gets thrown when
/// reading an EGT file goes wrong.
type EGTFileException(msg) = inherit exn(msg)

/// <summary>An entry of an EGT file.</summary>
/// <exclude/>
[<Struct; IsReadOnly; RequireQualifiedAccess>]
#if DEBUG
type Entry =
#else
type internal Entry =
#endif
    /// The entry has no data.
    | Empty
    /// The entry has a byte.
    | Byte of byteValue: byte
    /// The entry has a boolean value.
    | Boolean of boolValue: bool
    /// The entry has an unsigned 32-bit integer.
    | UInt32 of intValue: uint32
    /// The entry has a string.
    | String of stringValue: string
    [<NoDynamicInvocation>]
    static member inline Int x = UInt32 <| uint32 x

/// Functions to help EGT file readers.
[<AutoOpen>]
module internal EGTReaderUtilities =

    /// Raises an exception indicating that something
    /// went wrong with reading an EGT file.
    let invalidEGT() = raise <| EGTFileException "Invalid EGT file."

    /// Like `invalidEGT`, but allows specifying a formatted message.
    let invalidEGTf fmt = Printf.ksprintf (EGTFileException >> raise) fmt

    /// Raises an error if a read-only span's
    /// length is different than the expected.
    let lengthMustBe (m: ReadOnlySpan<_>) expectedLength =
        if m.Length <> expectedLength then
            invalidEGTf "Length must have been %d but was %d." expectedLength m.Length

    /// Raises an error if a read-only span's
    /// length is less than the expected.
    let lengthMustBeAtLeast (m: ReadOnlySpan<_>) expectedLength =
        if m.Length < expectedLength then
            invalidEGTf "Length must have been at least %d but was %d." expectedLength m.Length

    // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
    // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
    // FFFFFFfFFFFFFF
    let wantEmpty (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Empty -> () | _ -> invalidEGTf "Invalid entry, expecting Empty."
    let wantByte (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Byte x -> x | _ -> invalidEGTf "Invalid entry, expecting Byte."
    let wantBoolean (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.Boolean x -> x | _ -> invalidEGTf "Invalid entry, expecting Boolean."
    let wantUInt32 (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.UInt32 x -> x | _ -> invalidEGTf "Invalid entry, expecting Integer."
    let wantChar x idx = wantUInt32 x idx |> char
    let wantString (x: ReadOnlySpan<_>) idx =
        match x.[idx] with | Entry.String x -> x | _ -> invalidEGTf "Invalid entry, expecting String."

/// A class that reads EGT files from a stream.
type internal EGTReader(stream, [<Optional; DefaultParameterValue(false)>] leaveOpen) =

    let br = new BinaryReader(stream, Encoding.UTF8, leaveOpen)
    let mutable buffer = ArrayPool.Shared.Rent(128)
    let mutable length = 0

    /// Reads a null-terminated string, encoded
    /// with the UTF-16 character set from a binary reader.
    /// Commonly used to read the header of an EGT file.
    let readNullTerminatedString isHeader =
        let sr = StringBuilder(128)
        let mutable c = br.ReadUInt16()
        while c <> 0us do
            if isHeader && sr.Length >= sr.Capacity then
                invalidEGT()
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

    let header = readNullTerminatedString true

    /// Reads an EGT file entry from a binary reader.
    let readEntry() =
        match br.ReadByte() with
        | 'E'B -> Entry.Empty
        | 'b'B -> Entry.Byte(br.ReadByte())
        | 'B'B -> Entry.Boolean(br.ReadByte() <> 0uy)
        | 'I'B -> Entry.Int(br.ReadUInt16())
        | 'S'B -> Entry.String(readNullTerminatedString false)
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
            | x -> invalidEGTf "Invalid record code, read %x." x
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
            raise (ObjectDisposedException("buffer"))
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

/// A class that writes EGT files to a stream.
/// An EGT file consists of a header -a UTF-16-encoded string at the start of
/// the file- and a series of records. A record contains entries that can
/// contain a byte, a boolean, an unsigned integer, a string, or nothing.
type internal EGTWriter(stream, header, [<Optional; DefaultParameterValue(false)>] leaveOpen) =

    let w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen)

    let buffer = ResizeArray()

    /// Writes a null-terminated string, encoded
    /// with the UTF-16 character set to a binary writer.
    let writeNullTerminatedString str =
        // God, why does String.length null return zero?
        if isNull str then
            nullArg "str"
        for i = 0 to String.length str - 1 do
            // It is documented that binary writers
            // write in little-endian format; exactly what we want.
            w.Write(uint16 str.[i])
        w.Write(0us)

    do
        if isNull header then
            nullArg "header"
        writeNullTerminatedString header

    /// An implementation of the BinaryWriter.Write7BitEncodedInt
    /// method. Adapted from .NET Core's sources.
    let rec write7BitEncodedInt x =
        if x >= 0x80u then
            w.Write(byte x ||| 0x80uy)
            write7BitEncodedInt (x >>> 7)
        else
            w.Write(byte x)

    /// Writes an `Entry` to a binary writer.
    let writeEntry (e: inref<_>) =
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
            write7BitEncodedInt x
        | Entry.String str ->
            if isNull str then
                invalidOp "Cannot write a null string in an EGT file."
            w.Write('s'B)
            w.Write(str)

    let writeRecord (record: ReadOnlySpan<_>) =
        w.Write('m'B)
        write7BitEncodedInt (uint32 record.Length)
        for i = 0 to record.Length - 1 do
            writeEntry &record.[i]
        w.Flush()

    /// Appends an `Entry` to the next record.
    /// This record is not written until `FinishPendingRecord` is called.
    member _.WriteEntry e = buffer.Add e
    /// Appends an empty entry to the next record.
    member x.WriteEmpty() = x.WriteEntry Entry.Empty
    /// Appends an entry with a byte to the next record.
    member x.WriteByte b = x.WriteEntry(Entry.Byte b)
    /// Appends an entry with a boolean to the next record.
    member x.WriteBoolean b = x.WriteEntry(Entry.Boolean b)
    /// Appends an entry with an unsigned 32-bit integer to the next record.
    member x.WriteUInt32 i = x.WriteEntry(Entry.UInt32 i)
    /// Appends an entry with any integer to the next record.
    /// That integer must fit in an unsigned 32-bit integer.
    [<NoDynamicInvocation>]
    member inline x.WriteInt i = x.WriteEntry(Entry.Int i)
    /// Appends an entry with a string to the next record.
    member x.WriteString s = x.WriteEntry(Entry.String s)
    /// Writes a record to the stream that contains the entries added
    /// by the `Write***` functions, in order they were called.
    member _.FinishPendingRecord() =
        #if NET
        let span = Span.op_Implicit(CollectionsMarshal.AsSpan(buffer))
        writeRecord span
        buffer.Clear()
        #else
        let count = buffer.Count
        let mem = ArrayPool.Shared.Rent count
        try
            buffer.CopyTo(mem)
            writeRecord (ReadOnlySpan(mem, 0, count))
            buffer.Clear()
        finally
            ArrayPool.Shared.Return(mem, true)
        #endif
    /// Directly writes a record. If there are pending
    /// entries this function throws an exception.
    member _.WriteFullRecord record =
        if buffer.Count > 0 then
            invalidOp "Cannot write a full record when an unfinished one is about to be written."
        writeRecord record

    interface IDisposable with
        /// Disposes the class' buffers and optionally the underlying stream.
        /// If there are pending entries this function throws an exception.
        member _.Dispose() =
            if buffer.Count > 0 then
                invalidOp "Cannot dispose an EGT writer with an unfinished pending record."
            buffer.Clear()
            buffer.Capacity <- 0
            w.Dispose()
