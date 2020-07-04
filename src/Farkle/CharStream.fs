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
open System.IO
open System.Runtime.InteropServices

/// The bridge between a character stream and the post-processor API.
type ITransformer<'sym> =
    /// <summary>Converts a terminal into an arbitrary object.</summary>
    abstract Transform: 'sym * Position * ReadOnlySpan<char> -> obj

[<Struct>]
/// An continuous range of characters that is
/// stored by its starting position and ending index.
type internal CharSpan = {
    StartingPosition: Position
    IndexTo: uint64
}
with
    /// The length of the span. It can never be zero.
    member x.Length = x.IndexTo - x.StartingPosition.Index + 1UL |> int
    override x.ToString() = sprintf "[%d,%d]" x.StartingPosition.Index x.IndexTo

/// The source a `CharStream` reads characters from.
[<AbstractClass>]
type private CharStreamSource() =
    /// Ensures that all characters from `startingIndex` to `idx` are
    /// available for reading. Returns false when input ends or when
    /// TODO: `startingIndex` is before the first character stored on the buffer.
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
    /// To be overriden on sources that require it.
    abstract Dispose: unit -> unit
    default __.Dispose () = ()
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
    do nullCheck "reader" reader
    let mutable buffer = Array.zeroCreate bufferSize
    let mutable bufferFirstCharacterIndex = 0UL
    let mutable nextReadIndex = 0UL
    let checkDisposed() =
        if isNull buffer then
            raise <| ObjectDisposedException("Cannot use a dynamic block character stream after being disposed.")
    let growBuffer newLength =
        Array.Resize(&buffer, newLength)
    member private _.BufferContentLength = nextReadIndex - bufferFirstCharacterIndex |> int
    override _.LengthSoFar =
        checkDisposed()
        nextReadIndex
    override db.TryExpandPastIndex(startingIndex, idx) =
        checkDisposed()
        // The character we want to read is already in memory. Easy stuff.
        if idx < nextReadIndex then
            true
        // The character we want to read is the next one to be read.
        elif idx = nextReadIndex then
            // The buffer might be full however.
            if db.BufferContentLength = buffer.Length then
                // If not all characters in the buffer are needed, we move those we need to the start.
                if bufferFirstCharacterIndex <> startingIndex then
                    let importantCharStart = int <| startingIndex - bufferFirstCharacterIndex
                    let importantCharLength = int <| nextReadIndex - startingIndex
                    Array.blit buffer importantCharStart buffer 0 importantCharLength
                    bufferFirstCharacterIndex <- startingIndex
                else
                    // Otherwise we make the buffer larger.
                    growBuffer (buffer.Length * 2)
            let bufferContentLength = db.BufferContentLength
            // It's now time to read more characters.
            let nRead = reader.Read(buffer, bufferContentLength, buffer.Length - bufferContentLength)
            nextReadIndex <- nextReadIndex + uint64 nRead
            // If zero characters were actually read, we have reached the end.
            nRead <> 0
        // We cannot read past the first character that has not been read.
        // This is how the CharStream works; you have to read one character at a time.
        // TODO: Make it read more than one character away from it.
        else
            failwith "Cannot expand past the final character in the buffer."
    override db.GetAllCharactersAfterIndex idx =
        checkDisposed()
        let startIndex = idx - bufferFirstCharacterIndex |> int
        ReadOnlySpan(buffer, startIndex, db.BufferContentLength - startIndex)
    override _.GetSpanForCharacters(startIndex, length) =
        checkDisposed()
        let startIndex = startIndex - bufferFirstCharacterIndex |> int
        ReadOnlySpan(buffer, startIndex, length)
    override _.Dispose() =
        buffer <- null

/// A data structure that supports efficient access to a
/// read-only sequence of characters. It is not thread-safe.
/// And disposing it also disposes the underlying text reader (if exists).
type CharStream = private {
    /// The stream's source.
    Source: CharStreamSource
    /// The index of the first element that must be retained in memory
    /// because it is going to be used to generate a token.
    mutable StartingIndex: uint64
    mutable _CurrentPosition: Position
    /// [omit]
    mutable _LastTokenPosition: Position
}
with
    static member private Create(src) = {
        Source = src
        StartingIndex = 0UL
        _CurrentPosition = Position.Initial
        _LastTokenPosition = Position.Initial
    }
    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    static member Create(mem) = CharStream.Create(new StaticBlockSource(mem))
    /// Creates a `CharStream` from a string.
    static member Create(str: string) =
        nullCheck "str" str
        CharStream.Create(str.AsMemory())
    /// Creates a `CharStream` that lazily reads from a `TextReader`.
    /// The size of the stream's internal character buffer can be optionally specified.
    /// `CharStreams` made from this function must be `Dispose`d.
    /// The `TextReader` inside must be separately disposed as well.
    static member Create(reader, [<Optional; DefaultParameterValue(256)>] bufferSize: int) =
        if bufferSize <= 0 then
            invalidArg "bufferSize" "The buffer size cannot be negative."
        CharStream.Create(new DynamicBlockSource(reader, bufferSize))
    member internal x.CurrentIndex = x.CurrentPosition.Index
    /// Gets the stream's current position.
    /// Reading the stream for a new token starts from here.
    member x.CurrentPosition: inref<_> = &x._CurrentPosition
    /// The starting position of the last token that was generated.
    member x.LastTokenPosition: inref<_> = &x._LastTokenPosition
    /// A read-only span of characters that contains all characters from `CurrentPosition`.
    /// The first character of a new token is always in the zeroth position.
    member internal x.CharacterBuffer = x.Source.GetAllCharactersAfterIndex x.CurrentIndex
    /// Converts an offset relative to the current
    /// position to an absolute character index.
    member internal x.ConvertOffsetToIndex ofs =
        if ofs < 0 then
            invalidArg "ofs" "The offset cannot be negative"
        x.CurrentIndex + uint64 ofs
    /// Tries to load the `ofs`th character after the stream's current position.
    /// If input ended, returns false. This function invalidates the stream's
    /// character buffer but keeps the indices of the new buffer valid.
    member internal x.TryExpandPastOffset ofs =
        x.Source.TryExpandPastIndex(x.StartingIndex, x.ConvertOffsetToIndex ofs)
    interface IDisposable with
        member x.Dispose() = (x.Source :> IDisposable).Dispose()

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module internal CharStream =

    /// Creates a `CharSpan` that contains the next `idxTo`
    /// characters of a `CharStream` from its position.
    /// You must call this function _before_ calling `advance`.
    /// Despite the name, nothing happens if you don't unpin it.
    [<CompiledName("PinSpan")>]
    let pinSpan (cs: CharStream) idxTo = {
        StartingPosition = cs.CurrentPosition
        IndexTo = idxTo
    }

    /// Creates a new `CharSpan` that spans one
    /// character more than the given one.
    /// A `CharStream` must also be given to validate
    /// its length so that out-of-bounds errors are prevented.
    [<CompiledName("ExtendSpanByOne")>]
    let extendSpanByOne (cs: CharStream) span =
        if span.IndexTo < cs.Source.LengthSoFar then
            {span with IndexTo = span.IndexTo + 1UL}
        else
            failwith "Trying to extend a character span by one character past these that were already read."

    /// Updates the ending index of a `CharSpan`.
    /// It must not exceed the stream's number of
    /// currently read characters.
    [<CompiledName("ExtendSpan")>]
    let extendSpan (cs: CharStream) span endIdx =
        if endIdx < cs.Source.LengthSoFar then
            {span with IndexTo = endIdx}
        else
            failwithf "Trying to extend a character span from a stream with currently %d characters to %d."
                cs.Source.LengthSoFar endIdx

    /// Returns the position of the character at `idx`.
    /// It cannot be less than the stream's index
    /// of the current position.
    [<CompiledName("GetPositionAtIndex")>]
    let getPositionAtIndex (cs: CharStream) idx =
        if idx >= cs.CurrentIndex then
            // If the index is equal to the current index,
            // we don't want to advance the position at all.
            // In this case, the span would be empty.
            let len = idx - cs.CurrentIndex |> int
            let span = cs.Source.GetSpanForCharacters(cs.CurrentIndex, len)
            cs.CurrentPosition.Advance(span)
        else
            failwithf "Cannot get the position of the character at index %d (the stream is at %d)," idx cs.CurrentIndex

    /// Advances a `CharStream`'s position just after the given index.
    /// Further reads should start one character after it.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, the characters before the index can be marked to be released from memory.
    [<CompiledName("Advance")>]
    let advance doUnpin (cs: CharStream) idxTo =
        // We want to place the current position just after the index.
        cs._CurrentPosition <- getPositionAtIndex cs (idxTo + 1UL)
        if doUnpin then
            cs.StartingIndex <- cs.CurrentIndex

    /// Advances a `CharStream`'s position by one character.
    /// Further reads should start from the next one.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character and all before it can be marked to be released from memory.
    [<CompiledName("AdvanceByOne")>]
    let advanceByOne doUnpin (cs: CharStream) =
        if cs.CurrentIndex < uint64 cs.Source.LengthSoFar then
            advance doUnpin cs cs.CurrentIndex
        else
            failwith "Cannot consume a character stream past its end."

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the
    /// span might be freed from memory, so this function must not be used twice.
    [<CompiledName("UnpinSpanAndGenerate")>]
    let unpinSpanAndGenerateObject symbol (transformer: ITransformer<'symbol>) cs
        ({StartingPosition = {Index = idxStart}; IndexTo = idxEnd} as charSpan) =
        if cs.StartingIndex <= idxStart && cs.Source.LengthSoFar > idxEnd then
            cs.StartingIndex <- idxEnd + 1UL
            let span = cs.Source.GetSpanForCharacters(idxStart, charSpan.Length)
            cs._LastTokenPosition <- charSpan.StartingPosition
            transformer.Transform(symbol, cs._LastTokenPosition, span)
        else
            failwithf "Trying to read the character span %O, from a stream that was last read at %d."
                charSpan cs.StartingIndex
