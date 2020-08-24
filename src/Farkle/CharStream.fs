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
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices

/// The bridge between a character stream and the post-processor API.
type ITransformer<'sym> =
    /// <summary>Converts a terminal into an arbitrary object.</summary>
    abstract Transform: 'sym * Position * ReadOnlySpan<char> -> obj

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
type private DynamicBlockSource(reader: TextReader, bufferSize) =
    inherit CharStreamSource()
    do
        nullCheck "reader" reader
        if bufferSize <= 0 then
            raise <| ArgumentOutOfRangeException("bufferSize", bufferSize,
                "The buffer size cannot be negative or zero.")
    let mutable buffer = Array.zeroCreate bufferSize
    let mutable bufferFirstCharacterIndex = 0UL
    let mutable nextReadIndex = 0UL
    let checkDisposed() =
        if isNull buffer then
            raise <| ObjectDisposedException("Cannot use a dynamic block character stream after being disposed.")
    let growBuffer newLength =
        Array.Resize(&buffer, newLength)
    let getBufferContentLength() = nextReadIndex - bufferFirstCharacterIndex |> int
    let rec tryExpandPastIndex startingIndex idx =
        checkDisposed()
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
                    Array.blit buffer importantCharStart buffer 0 importantCharLength
                    bufferFirstCharacterIndex <- startingIndex
                else
                    // Otherwise we make the buffer larger.
                    growBuffer (buffer.Length * 2)
            let bufferContentLength = getBufferContentLength()
            // It's now time to read more characters.
            let nRead = reader.Read(buffer, bufferContentLength, buffer.Length - bufferContentLength)
            nextReadIndex <- nextReadIndex + uint64 nRead
            // If no new characters were read, we have reached the end of the file.
            // Otherwise we check again if the character we want is available.
            // We will then either return or expand the buffer again.
            nRead <> 0 && tryExpandPastIndex startingIndex idx
    override _.TryExpandPastIndex(startingIndex, idx) =
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
        buffer <- null

/// A data structure that supports efficient access to a
/// read-only sequence of characters. It is not thread-safe.
/// And disposing it also disposes the underlying text reader (if exists).
type CharStream private(source: CharStreamSource) =
    /// The index of the first element that must be retained in memory
    /// because it is going to be used to generate a token.
    let mutable startingIndex = 0UL
    let mutable currentPosition = Position.Initial
    let mutable lastTokenPosition = Position.Initial
    let checkOfsetPositive ofs =
        if ofs < 0 then
            raise(ArgumentOutOfRangeException("ofs", ofs, "The offset cannot be negative."))
    /// Converts an offset relative to the current
    /// position to an absolute character index.
    let convertOffsetToIndex ofs = currentPosition.Index + uint64 ofs
    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    new(mem) = new CharStream(new StaticBlockSource(mem))
    /// Creates a `CharStream` from a string.
    new (str: string) =
        nullCheck "str" str
        new CharStream(str.AsMemory())
    /// Creates a `CharStream` that lazily reads the characters from a `TextReader`.
    /// The size of the stream's internal character buffer can be optionally specified.
    new(reader, [<Optional; DefaultParameterValue(256)>] bufferSize: int) =
        if bufferSize <= 0 then
            invalidArg "bufferSize" "The buffer size cannot be negative or zero."
        new CharStream(new DynamicBlockSource(reader, bufferSize))
    member internal _.CurrentIndex = currentPosition.Index
    /// Gets the stream's current position.
    /// Reading the stream for a new token starts from here.
    member _.CurrentPosition: inref<_> = &currentPosition
    /// The starting position of the last token that was generated.
    member _.LastTokenPosition: inref<_> = &lastTokenPosition
    /// A read-only span of characters that contains all characters from `CurrentPosition`.
    /// The first character of a new token is always in the zeroth position.
    member internal _.CharacterBuffer = source.GetAllCharactersAfterIndex currentPosition.Index
    /// Tries to load the `ofs`th character after the stream's current position.
    /// If input ended, returns false. This function invalidates the stream's
    /// character buffer but keeps the indices of the new buffer valid.
    member internal _.TryExpandPastOffset ofs =
        checkOfsetPositive ofs
        source.TryExpandPastIndex(startingIndex, convertOffsetToIndex ofs)
    /// Returns the position of the character at `ofs`
    /// characters after the current position.
    /// It cannot be negative.
    member internal _.GetPositionAtOffset ofs =
        checkOfsetPositive ofs
        let span = source.GetSpanForCharacters(currentPosition.Index, ofs)
        currentPosition.Advance span
    /// Advances a `CharStream`'s current position just after the
    /// next `ofs`th character from the stream's current position.
    /// This function invalidates the indices for the stream's `CharacterBuffer`.
    /// Optionally, the characters can be marked to be released from memory.
    member internal x.AdvancePastOffset(ofs, doUnpin) =
        currentPosition <- x.GetPositionAtOffset(ofs + 1)
        if doUnpin then
            startingIndex <- currentPosition.Index
    /// Marks the start of the next token by setting the
    /// stream's last token position to the current one.
    member internal _.StartNewToken() = lastTokenPosition <- currentPosition
    /// Creates an arbitrary object from the characters
    /// between the `CharStream`'s last token position and its current position.
    /// After that call, the characters at and before the current position
    /// might be freed from memory, so this method must not be used twice.
    member internal x.FinishNewToken symbol (transformer: ITransformer<'TSymbol>) =
        let idxStart = lastTokenPosition.Index
        let idxEnd = x.CurrentIndex - 1UL
        let length = x.CurrentIndex - idxStart |> int
        if startingIndex <= idxStart && source.LengthSoFar > idxEnd then
            startingIndex <- idxEnd + 1UL
            let span = source.GetSpanForCharacters(idxStart, length)
            transformer.Transform(symbol, lastTokenPosition, span)
        else
            failwithf "Trying to read from %d to %d, from a stream that was last read at %d."
                idxStart idxEnd startingIndex
    interface IDisposable with
        member _.Dispose() = (source :> IDisposable).Dispose()
