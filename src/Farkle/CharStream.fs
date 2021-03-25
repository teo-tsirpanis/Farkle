// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.IO

open Farkle
open Farkle.Common
#if DEBUG
open Operators.Checked
#endif
open System
open System.Buffers
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices

/// The source a `CharStream` reads characters from.
[<AbstractClass>]
type private CharStreamSource() =
    /// Ensures that all characters from `startingIndex` to `idx` are
    /// available for reading. Returns false when input ends or when
    /// This is the only place when I/O occurs. After this call, more characters
    /// after the requested range might be available as well.
    abstract TryExpandPastIndex: startingIndex: uint64 * idx: uint64 -> bool
    /// Returns a read-only span of all characters that are
    /// available in memory from `startingIndex`, inclusive.
    abstract GetAllCharactersAfterIndex: startingIndex: uint64 -> ReadOnlySpan<char>
    /// Returns the length of the input, or at least the
    /// length of the input that has ever crossed the memory.
    /// In dynamic block streams, it doesn't mean
    /// that all these characters are still in memory.
    abstract LengthSoFar: uint64
    /// Returns a `ReadOnlySpan` containing the characters
    /// from the given index with the given length.
    abstract GetSpanForCharacters: idx: uint64 * len: int -> ReadOnlySpan<char>
    /// Disposes unmanaged resources using a well-known pattern.
    /// To be overridden on sources that require it.
    abstract Dispose: unit -> unit
    default _.Dispose () = ()
    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<Sealed>]
/// A source of a `CharStream` that stores
/// the characters in one contiguous area of memory.
type private StaticBlockSource(mem: ReadOnlyMemory<_>) =
    inherit CharStreamSource()
    let length = uint64 mem.Length
    override _.TryExpandPastIndex (_, idx) = idx < length
    override _.GetAllCharactersAfterIndex idx = mem.Span.Slice(int idx)
    override _.LengthSoFar = length
    override _.GetSpanForCharacters(startIndex, length) = mem.Span.Slice(int startIndex, length)

[<Sealed>]
type private DynamicBlockSource(reader: TextReader, leaveOpen, bufferSize) =
    inherit CharStreamSource()
    do
        nullCheck "reader" reader
        if bufferSize <= 0 then
            raise <| ArgumentOutOfRangeException("bufferSize", bufferSize,
                "The buffer size cannot be negative or zero.")
    let mutable buffer = ArrayPool.Shared.Rent bufferSize
    let mutable bufferFirstCharacterIndex = 0UL
    let mutable nextReadIndex = 0UL
    let checkDisposed() =
        if isNull buffer then
            raise <| ObjectDisposedException("Cannot use a dynamic block character stream after being disposed.")
    let growBuffer() =
        let newLength = buffer.Length * 2
        let bufferNew = ArrayPool.Shared.Rent newLength
        ReadOnlySpan(buffer).CopyTo(Span bufferNew)
        ArrayPool.Shared.Return(buffer)
        buffer <- bufferNew
    let getBufferContentLength() = nextReadIndex - bufferFirstCharacterIndex |> int
    let rec tryExpandPastIndex startingIndex idx =
        Debug.Assert(startingIndex >= bufferFirstCharacterIndex,
            "The starting index was behind the first character in the buffer.")
        Debug.Assert(idx >= startingIndex,
            "The index to expand to was behind the starting index.")
        // The character we want to read is already in memory. Easy stuff.
        if idx < nextReadIndex then
            true
        // The character we want to read is the next one to be read.
        else
            // The buffer might be full however.
            if getBufferContentLength() = buffer.Length then
                // If not all characters in the buffer are needed, we move those we need to the start.
                if bufferFirstCharacterIndex <> startingIndex then
                    let importantCharStart = int <| startingIndex - bufferFirstCharacterIndex
                    let importantCharLength = int <| nextReadIndex - startingIndex
                    ReadOnlySpan(buffer, importantCharStart, importantCharLength).CopyTo(Span buffer)
                    bufferFirstCharacterIndex <- startingIndex
                else
                    // Otherwise we make the buffer larger.
                    growBuffer()
            let bufferContentLength = getBufferContentLength()
            // It's now time to read more characters.
            let nRead = reader.Read(buffer, bufferContentLength, buffer.Length - bufferContentLength)
            nextReadIndex <- nextReadIndex + uint64 nRead
            // If no new characters were read, we have reached the end of the file.
            // Otherwise we check again if the character we want is available.
            // We will then either return or expand the buffer again.
            nRead <> 0 && tryExpandPastIndex startingIndex idx
    override _.TryExpandPastIndex(startingIndex, idx) =
        checkDisposed()
        tryExpandPastIndex startingIndex idx
    override _.LengthSoFar =
        checkDisposed()
        nextReadIndex
    override _.GetAllCharactersAfterIndex idx =
        checkDisposed()
        let startIndex = idx - bufferFirstCharacterIndex |> int
        ReadOnlySpan(buffer, startIndex, getBufferContentLength() - startIndex)
    override _.GetSpanForCharacters(startIndex, length) =
        checkDisposed()
        let startIndex = startIndex - bufferFirstCharacterIndex |> int
        ReadOnlySpan(buffer, startIndex, length)
    override _.Dispose() =
        ArrayPool.Shared.Return buffer
        buffer <- null
        if not leaveOpen then
            reader.Dispose()

/// A data structure that supports efficient access to a
/// read-only sequence of characters. It is not thread-safe.
type CharStream private(source: CharStreamSource) =
    let mutable objectStore: IDictionary<_,_> = null
    // The dynamic block source must retain the
    // characters after this position in memory.
    let mutable startingPosition = Position.Initial
    let mutable currentPosition = Position.Initial
    let checkOfsetPositive ofs =
        if ofs < 0 then
            raise(ArgumentOutOfRangeException("ofs", ofs, "The offset cannot be negative."))
    /// Converts an offset relative to the current
    /// position to an absolute character index.
    let convertOffsetToIndex ofs = currentPosition.Index + uint64 ofs
    let updateTokenStartPosition() =
        startingPosition <- currentPosition
    /// <summary>Creates a <see cref="CharStream"/> from a
    /// <see cref="ReadOnlyMemory{Char}"/>.</summary>
    new(mem) = new CharStream(new StaticBlockSource(mem))
    /// <summary>Creates a <see cref="CharStream"/> from a string.</summary>
    new (str: string) =
        nullCheck "str" str
        new CharStream(str.AsMemory())
    /// <summary>Creates a <see cref="CharStream"/> that lazily reads
    /// its characters from a <see cref="TextReader"/>.</summary>
    /// <param name="reader">The text reader to read characters from.</param>
    /// <param name="leaveOpen">Whether to keep the underlying text reader
    /// open when the character stream gets disposed.</param>
    /// <param name="bufferSize">The size of the stream's
    /// internal character buffer. It has a default value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bufferSize"/> is negative or zero</exception>
    new(reader, [<Optional>] leaveOpen, [<Optional; DefaultParameterValue(256)>] bufferSize: int) =
        if bufferSize <= 0 then
            raise (ArgumentOutOfRangeException("bufferSize", bufferSize, "The buffer size cannot be negative or zero."))
        new CharStream(new DynamicBlockSource(reader, leaveOpen, bufferSize))
    /// The starting position of the last token that was generated.
    member internal _.TokenStartPosition: inref<_> = &startingPosition
    /// The position of the next character the stream has to read.
    member _.CurrentPosition: inref<_> = &currentPosition
    /// <inheritdoc cref="ITokenizerContext.ObjectStore"/>
    member _.ObjectStore =
        if isNull objectStore then
            objectStore <- Dictionary(StringComparer.Ordinal)
        objectStore
    /// A read-only span of characters that contains all
    /// available characters at and after the stream's current position.
    member _.CharacterBuffer = source.GetAllCharactersAfterIndex currentPosition.Index
    /// <summary>Tries to load the <paramref name="ofs"/>th character after the stream's
    /// current position. If it does not exist, returns false. This function invalidates
    /// the stream's <see cref="CharacterBuffer"/> but keeps the indices of the new buffer
    /// valid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ofs"/> is negative.</exception>
    member _.TryExpandPastOffset ofs =
        checkOfsetPositive ofs
        source.TryExpandPastIndex(startingPosition.Index, convertOffsetToIndex ofs)
    /// <summary>Returns the position of the character at <paramref name="ofs"/>
    /// characters after the current position.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ofs"/> is negative.</exception>
    member _.GetPositionAtOffset ofs =
        checkOfsetPositive ofs
        let span = source.GetSpanForCharacters(currentPosition.Index, ofs)
        currentPosition.Advance span
    /// <summary>Advances the stream's current position by <paramref name="ofs"/>
    /// characters. This function invalidates the indices for the stream's
    /// <see cref="CharacterBuffer"/> and its characters might be released
    /// from memory.</summary>
    /// <remarks>Both Windows line ending chatacters must be advanced at the same
    /// time, otherwise the stream's current position will be incorrect.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ofs"/> is negative.</exception>
    member x.AdvanceBy ofs =
        checkOfsetPositive ofs
        if ofs <> 0 then
            x.AdvancePastOffset(ofs - 1, true)
    /// Advances the stream's current position just after the
    /// next `ofs`th character from the stream's current position.
    /// This function invalidates the indices for the stream's `CharacterBuffer`.
    /// A call of this function with `doUnpin` set to false will not release the
    /// characters from memory but requires but requires to be paired witha call
    /// to CreateToken.
    member internal x.AdvancePastOffset(ofs, doUnpin) =
        currentPosition <- x.GetPositionAtOffset(ofs + 1)
        if doUnpin then
            updateTokenStartPosition()
    /// Creates an arbitrary object from the characters
    /// between the `CharStream`'s last token position and its current position.
    /// After that call, the characters at and before the current position
    /// might be freed from memory, so this method must not be used twice.
    member internal x.CreateToken symbol (transformer: ITransformer<'TSymbol>) =
        let idxStart = startingPosition.Index
        let length = currentPosition.Index - idxStart |> int
        let span = source.GetSpanForCharacters(idxStart, length)
        let result = transformer.Transform(symbol, x, span)
        updateTokenStartPosition()
        result
    interface ITransformerContext with
        member _.StartPosition = &startingPosition
        member _.EndPosition = &currentPosition
        member x.ObjectStore = x.ObjectStore
    interface IDisposable with
        member _.Dispose() = (source :> IDisposable).Dispose()
