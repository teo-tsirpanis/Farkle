// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.IO

open Farkle
#if DEBUG
open Operators.Checked
#endif
open System
open System.IO
open System.Runtime.InteropServices

/// The bridge between a character stream and the post-processor API.
type ITransformer<'sym> =
    /// <summary>Converts a terminal into an arbitrary object.</summary>
    /// <remarks>In case of an insignificant token, implementations can return <c>null</c></remarks>.
    abstract Transform: 'sym * Position * ReadOnlySpan<char> -> obj

[<Struct>]
/// An continuous range of characters that is
/// stored by its starting position and ending index.
type CharSpan = private {
    StartingPosition: Position
    IndexTo: uint64
}
with
    /// The length of the span.
    /// It can never be zero.
    member x.Length = x.IndexTo - x.StartingPosition.Index + 1UL |> int
    override x.ToString() = sprintf "[%d,%d]" x.StartingPosition.Index x.IndexTo

/// The source a `CharStream` reads characters from.
[<AbstractClass>]
type private CharStreamSource() =
    /// Gets a specified character by index; if it exists in memory, or raises an exception.
    abstract Item: uint64 -> char
    /// Returns the length of the input; or at least the
    /// length of the input that has ever crossed the memory.
    /// In dynamic block streams, it doesn't mean that all these characters are still in memory.
    abstract LengthSoFar: uint64
    /// Reads the character at the specified index, places it into the outref, and moves the
    /// index one position forward. Returns `false` when input ended. The first parameter
    /// is used for the dynamic block source.
    abstract ReadNextCharacter: startingIndex: uint64 * idx: byref<uint64> * c: outref<char> -> bool
    /// Returns a `ReadOnlySpan` containing the characters from the given index with the given length.
    /// The characters in the span are not bound to be released from memory after that call;
    /// this is the responsibility of the unpinning functions.
    abstract GetSpanForCharacters: uint64 * int -> ReadOnlySpan<char>
    /// Disposes unmanaged resources using a well-known pattern.
    /// To be overriden on sources that require it.
    abstract Dispose: bool -> unit
    default __.Dispose _ = ()
    interface IDisposable with
        member x.Dispose() =
            x.Dispose(true)
            GC.SuppressFinalize(x)

[<Sealed>]
/// A source of a `CharStream` that stores
/// the characters in one continuous area of memory.
type private StaticBlockSource(mem: ReadOnlyMemory<_>) =
    inherit CharStreamSource()
    let length = uint64 mem.Length
    override __.Item idx = mem.Span.[int idx]
    override __.LengthSoFar = length
    override __.ReadNextCharacter(_, idx, c) =
        if idx < uint64 mem.Length then
            c <- mem.Span.[int idx]
            idx <- idx + 1UL
            true
        else
            false
    override __.GetSpanForCharacters(startIndex, length) = mem.Span.Slice(int startIndex, length)

[<Sealed>]
/// A representation of a `CharStream` that lazily
/// reads charactes from a `TextReader` when needed
/// and might unload them when not.
type private DynamicBlockSource(reader: TextReader, bufferSize) =
    inherit CharStreamSource()
    /// Whether the `Dispose` method has been called.
    /// Using this class is prohibited afterwards.
    let mutable disposed = false
    /// A character array where the characters that get read, but are still needed are stored.
    let mutable buffer = Array.zeroCreate bufferSize
    /// The index of the first element in the buffer.
    let mutable bufferFirstCharacterIndex = 0UL
    /// The index of the next character to be read.
    /// Alternatively, how many character have been read so far.
    let mutable nextReadIndex = 0UL
    member private __.CheckDisposed() =
        if disposed then
            raise <| ObjectDisposedException("Cannot use a dynamic block character stream after being disposed.")
    member private __.IsBufferFull = uint64 buffer.Length = nextReadIndex - bufferFirstCharacterIndex
    override db.Item idx =
        db.CheckDisposed()
        if idx >= bufferFirstCharacterIndex && idx < nextReadIndex then
            let position = int <| idx - bufferFirstCharacterIndex
            buffer.[position]
        elif nextReadIndex = 0UL then
            failwithf "Trying to get character at %d, while nothing has been read yet" idx
        else
            failwithf "Trying to get character at %d, while only characters from %d to %d have been loaded"
                idx bufferFirstCharacterIndex <| nextReadIndex - 1UL
    override db.LengthSoFar =
        db.CheckDisposed()
        nextReadIndex
    override db.ReadNextCharacter(startingIndex, idx, c) =
        db.CheckDisposed()
        // The character we want to read is already in memory. Easy stuff.
        if idx < nextReadIndex then
            c <- db.[idx]
            idx <- idx + 1UL
            true
        // The character we want to read is the next one to be read.
        elif idx = nextReadIndex then
            // The buffer might be full however.
            if db.IsBufferFull then
                // If not all characters in the buffer are needed, we move those we need to the start.
                if bufferFirstCharacterIndex <> startingIndex then
                    let importantCharStart = int <| startingIndex - bufferFirstCharacterIndex
                    let importantCharLength = int <| nextReadIndex - startingIndex
                    Array.blit buffer importantCharStart buffer 0 importantCharLength
                    bufferFirstCharacterIndex <- startingIndex
                // Otherwise, we double the buffer's size. It is doubled to achieve amortized constant complexity.
                // But to reach the point of growing it, many times, we must have huge terminals. The default
                // buffer size is some hundreds of characters.
                else
                    Array.Resize(&buffer, buffer.Length * 2)
            // It's now time to read the next character.
            // We read each character at a time from the reader. It does not adversely
            // affect performance because the reader has its own buffer as well!
            let cRead = reader.Read()
            // There are still characters to read. We store this little
            // character in the buffer, and return it as well.
            if cRead <> -1 then
                nextReadIndex <- nextReadIndex + 1UL
                buffer.[int <| idx - bufferFirstCharacterIndex] <- char cRead
                c <- char cRead
                idx <- nextReadIndex
                true
            // We have reached the end.
            else
                false
        // We cannot read past the first character that has not been read.
        // This is how the CharStream works; you have to read one character at a time.
        else
            failwithf "Cannot read character at %d because the latest one was read at %d." idx nextReadIndex
    override db.GetSpanForCharacters(startIndex, length) =
        db.CheckDisposed()
        let startIndex = startIndex - bufferFirstCharacterIndex |> int
        ReadOnlySpan(buffer).Slice(startIndex, length)
    override __.Dispose(disposing) =
        disposed <- true
        if disposing then
            buffer <- null
        reader.Dispose()
    override db.Finalize() = db.Dispose(false)

/// A data structure that supports efficient access to a
/// read-only sequence of characters. It is not thread-safe.
/// And disposing it also disposes the underlying text reader (if exists).
type CharStream = private {
    /// The stream's source.
    Source: CharStreamSource
    /// The index of the first element that _must_ be retained in memory
    /// because it is going to be used to generate a token.
    mutable StartingIndex: uint64
    mutable _CurrentPosition: Position
    /// [omit]
    mutable _LastUnpinnedSpanPosition: Position
}
with
    static member private Create(src) = {
        Source = src
        StartingIndex = 0UL
        _CurrentPosition = Position.Initial
        _LastUnpinnedSpanPosition = Position.Initial
    }
    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    static member Create(mem) = CharStream.Create(new StaticBlockSource(mem))
    /// Creates a `CharStream` from a string.
    static member Create(str: string) = CharStream.Create(str.AsMemory())
    /// Creates a `CharStream` that lazily reads from a `TextReader`.
    /// The size of the stream's internal character buffer can be optionally specified.
    static member Create(reader, [<Optional; DefaultParameterValue(256)>] bufferSize: int) =
        CharStream.Create(new DynamicBlockSource(reader, bufferSize))
    member internal x.CurrentIndex = x.CurrentPosition.Index
    /// Gets the stream's current position.
    /// Reading the stream should start from here.
    member x.CurrentPosition = x._CurrentPosition
    /// The starting position of the last character span that was unpinned.
    member x.LastUnpinnedSpanPosition = x._LastUnpinnedSpanPosition
    /// Ensures that the character at the current position is loaded in memory.
    /// If it is not, and input has ended, returns `false`.
    member x.TryLoadFirstCharacter() =
        let len = x.Source.LengthSoFar
        // The character at the current index is behind the length so far.
        if x.CurrentIndex < len then
            true
        // The character at the current index is the next one to be read.
        elif x.CurrentIndex = len then
            let mutable c = '\uBABE'
            let mutable idx = x.CurrentIndex
            // The input might have ended. But if not, the length so far will be
            // increased by one, and subsequent calls to this function will
            // go to the first if clause.
            x.Source.ReadNextCharacter(x.StartingIndex, &idx, &c)
        // The current index has gone beyond the immediate next character to be read? Something is wrong!
        else
            failwith "The current index of a character stream cannot be larger than its length."
    /// The stream's character at its current position.
    /// Call this function only when `TryLoadFirstCharacter()` returns `true`.
    member x.FirstCharacter = x.Source.[x.CurrentIndex]
    interface IDisposable with
        member x.Dispose() = (x.Source :> IDisposable).Dispose()

/// A .NET delegate that is the interface between the `CharStream` API and the post-processor.
/// It accepts a generic type (a `Terminal` usually), the `Position` of the symbol, and a
/// `ReadOnlySpan` of characters that are going to be converted into an object.
/// This type is not an F# native function type, because of limitations while handling `ReadOnlySpan`s.
// That type is still used by the C# post-processor API. What a failure it was!
type CharStreamCallback<'symbol> = delegate of 'symbol * Position * ReadOnlySpan<char> -> obj

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module CharStream =

    /// Reads the `idx`th character of `cs`, places it in `c` and
    /// returns `true`, if there are more characters left to be read.
    /// Otherwise, returns `false`.
    [<CompiledName("Read")>]
    let readChar (cs: CharStream) (idx: byref<_>) (c: outref<_>) =
        if idx >= cs.CurrentIndex then
            cs.Source.ReadNextCharacter(cs.StartingIndex, &idx, &c)
        else
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been consumed." idx cs.CurrentIndex

    /// Creates a `CharSpan` that contains the next `idxTo`
    /// characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that
    /// the characters in the span stay in memory.
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

    /// Creates a new `CharSpan` from the union of two adjacent spans, i.e.
    /// that starts at the first's start, and ends at the second's end.
    [<CompiledName("ConcatSpans")>]
    let concatSpans span1 span2 =
        if span1.IndexTo + 1UL = span2.StartingPosition.Index then
            {span1 with IndexTo = span2.IndexTo}
        else
            failwithf "Trying to concatenate character span %O with %O." span1 span2

    /// Advances a `CharStream`'s position by one character.
    /// Next reads should start from that position.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character and all before it can be marked to be released from memory.
    [<CompiledName("AdvanceByOne")>]
    let advanceByOne doUnpin (cs: CharStream) =
        if cs.CurrentIndex < uint64 cs.Source.LengthSoFar then
            cs._CurrentPosition <- cs.CurrentPosition.Advance cs.FirstCharacter
            if doUnpin then
                cs.StartingIndex <- cs.CurrentIndex
        else
            failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// Next reads should start from that position.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, the characters before the span can be marked to be released from memory.
    [<CompiledName("Advance")>]
    let advance doUnpin (cs: CharStream) span =
        if cs.CurrentIndex = span.StartingPosition.Index then
            for _i = 1 to span.Length do
                advanceByOne doUnpin cs
        else
            failwithf "Trying to consume the character span %O, from a stream that was left at %d." span cs.CurrentIndex

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the
    /// span might be freed from memory, so this function must not be used twice.
    [<CompiledName("UnpinSpanAndGenerate")>]
    let unpinSpanAndGenerate symbol (transformer: ITransformer<'symbol>) cs
        ({StartingPosition = {Index = idxStart}; IndexTo = idxEnd} as charSpan) =
        if cs.StartingIndex <= idxStart && cs.Source.LengthSoFar > idxEnd then
            cs.StartingIndex <- idxEnd + 1UL
            let length = idxEnd - idxStart + 1UL |> int
            let span = cs.Source.GetSpanForCharacters(idxStart, length)
            cs._LastUnpinnedSpanPosition <- charSpan.StartingPosition
            transformer.Transform(symbol, cs._LastUnpinnedSpanPosition, span)
        else
            failwithf "Trying to read the character span %O, from a stream that was last read at %d." charSpan cs.StartingIndex

    let private toStringTransformer =
        {new ITransformer<unit> with member _.Transform(_, _, data) = box <| data.ToString()}

    /// Creates a string out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the span might be
    /// freed from memory, so this function must not be used twice.
    /// It is recommended to use the `unpinSpanAndGenerate` function
    /// to avoid excessive allocations, unless you specifically want a string.
    [<CompiledName("UnpinSpanAndGenerateString")>]
    let unpinSpanAndGenerateString cs c_span =
        let s =
            unpinSpanAndGenerate
                ()
                toStringTransformer
                cs
                c_span // Created by cable
        s :?> string

    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    let ofReadOnlyMemory mem = CharStream.Create(mem: ReadOnlyMemory<_>)

    /// Creates a `CharStream` from a string.
    let ofString (x: string) = x.AsMemory() |> ofReadOnlyMemory

    /// Creates a `CharStream` that lazily reads from a `TextReader`.
    /// The size of the stream's internal character buffer is specified.
    /// Also, the character stream can be disposed if the reader is no more needed.
    let ofTextReaderEx bufferSize textReader = CharStream.Create(textReader, bufferSize)

    /// Creates a `CharStream` from a `TextReader`.
    /// It can be disposed if the reader is no more needed.
    let ofTextReader textReader = CharStream.Create(textReader: TextReader)
