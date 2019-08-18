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

/// An continuous range of characters that is
/// stored by its starting position and ending index.
type CharSpan = private {
    LineFrom: uint64
    ColumnFrom: uint64
    IndexFrom: uint64
    IndexTo: uint64
}
with
    /// The position of the span's first character.
    member x.GetStartingPosition() = {Line = x.LineFrom; Column = x.ColumnFrom; Index = x.IndexFrom}
    /// The length of the span.
    /// It can never be zero.
    member x.Length = x.IndexTo - x.IndexFrom + 1UL |> int
    override x.ToString() = sprintf "[%d,%d]" x.IndexFrom x.IndexTo

/// A type pointing to a character in a character stream.
type [<Struct>] CharStreamIndex = private {Index: uint64}

/// A representation of a `CharStream`.
// Previously, this type was a discriminated union. I changed it to an abstract because
// pattern matches on it, which involve two type checks+casts seemed to be inferior to virtual calls.
// Besides, the CLR is more inclined to handle inheritance and polymorphism.
// And regardless, this layout clearly separates source-specific code from the rest of it.
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
    abstract ReadNextCharacter: uint64 * byref<CharStreamIndex> * outref<char> -> bool
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
/// A representation of a `CharStream` that stores
/// the characters in one continuous area of memory.
/// It is not recommended for large files.
type private StaticBlockSource(mem: ReadOnlyMemory<_>) =
    inherit CharStreamSource()
    let length = uint64 mem.Length
    override __.Item idx = mem.Span.[int idx]
    override __.LengthSoFar = length
    override __.ReadNextCharacter(_, idx, c) =
        if idx.Index < uint64 mem.Length then
            c <- mem.Span.[int idx.Index]
            idx <- {Index = idx.Index + 1UL}
            true
        else
            false
    override __.GetSpanForCharacters(startIndex, length) = mem.Span.Slice(int startIndex, length)

[<Sealed>]
/// A representation of a `CharStream` that lazily
/// loads input from a `TextReader` when it is needed
/// and unloads it when it is not.
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
        if idx.Index < nextReadIndex then
            c <- db.[idx.Index]
            idx <- {Index = idx.Index + 1UL}
            true
        // The character we want to read is the next one to be read.
        elif idx.Index = nextReadIndex then
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
                buffer.[int <| idx.Index - bufferFirstCharacterIndex] <- char cRead
                c <- char cRead
                idx <- {Index = nextReadIndex}
                true
            // We have reached the end.
            else
                false
        // We cannot read past the first character that has not been read.
        // This is how the CharStream works; you have to read one character at a time.
        else
            failwithf "Cannot read character at %d because the latest one was read at %d." idx.Index nextReadIndex
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

/// A data structure that supports efficient and copy-free access to a read-only sequence of characters.
/// It is not thread-safe. Also, if you created a `TextReader` to just use it with the stream, you can
/// dispose the stream instead of the reader.
type CharStream = private {
    /// The stream's source.
    Source: CharStreamSource
    /// The index of the first element that _must_ be retained in memory
    /// because it is going to be used to generate a token.
    mutable StartingIndex: uint64
    /// The line the stream is currently at.
    mutable CurrentLine: uint64
    /// The column the stream is currently at.
    mutable CurrentColumn: uint64
    /// The character index the stream is currently at.
    mutable CurrentIndex: uint64
    /// [omit]
    mutable _LastUnpinnedSpanPosition: Position
}
with
    static member private Create(src) = {
        Source = src
        StartingIndex = 0UL
        CurrentLine = Position.Initial.Line
        CurrentColumn = Position.Initial.Column
        CurrentIndex = Position.Initial.Index
        _LastUnpinnedSpanPosition = Position.Initial
    }
    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    static member Create(mem) = CharStream.Create(new StaticBlockSource(mem))
    /// Creates a `CharStream` from a string.
    static member Create(str: string) = CharStream.Create(str.AsMemory())
    /// Creates a `CharStream` that lazily reads from a `TextReader`.
    /// The size of the stream's internal character buffer can be optionally specified.
    static member Create(reader, [<Optional; DefaultParameterValue(256)>] bufferSize: int) = CharStream.Create(new DynamicBlockSource(reader, bufferSize))
    /// Gets the stream's current position.
    /// Reading the stream will start from here.
    member x.GetCurrentPosition() = {Line = x.CurrentLine; Column = x.CurrentColumn; Index = x.CurrentIndex}
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
            let mutable idx = {Index = x.CurrentIndex}
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
type CharStreamCallback<'symbol> = delegate of 'symbol * Position * ReadOnlySpan<char> -> obj

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module CharStream =

    /// Creates a `CharStreamIndex` from a `CharStream` that points to its current position.
    [<CompiledName("GetCurrentIndex")>]
    let getCurrentIndex (cs: CharStream) = {Index = cs.CurrentIndex}

    /// Reads the `idx`th character of `cs`, places it in `c` and returns `true`, if there are more characters left to be read.
    /// Otherwise, returns `false`.
    [<CompiledName("Read")>]
    let readChar cs (c: outref<_>) (idx: byref<_>) =
        if idx.Index >= cs.CurrentIndex then
            cs.Source.ReadNextCharacter(cs.StartingIndex, &idx, &c)
        else
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been consumed." idx.Index cs.CurrentIndex

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    [<CompiledName("PinSpan")>]
    let pinSpan cs ({CharStreamIndex.Index = idxTo}) = {
        LineFrom = cs.CurrentLine
        ColumnFrom = cs.CurrentColumn
        IndexFrom = cs.CurrentIndex
        IndexTo = idxTo
    }

    /// Creates a new `CharSpan` that spans one character more than the given one.
    /// A `CharStream` must also be given to validate its length so that out-of-bounds errors are prevented.
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
        if span1.IndexTo + 1UL = span2.IndexFrom then
            {span1 with IndexTo = span2.IndexTo}
        else
            failwithf "Trying to concatenate character span %O with %O." span1 span2

    /// Advances a `CharStream`'s position by one character.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character and all before it can be marked to be released from memory.
    [<CompiledName("AdvanceByOne")>]
    let advanceByOne doUnpin (cs: CharStream) =
        if cs.CurrentIndex < uint64 cs.Source.LengthSoFar then
            Position.AdvanceImpl(cs.FirstCharacter, &cs.CurrentLine, &cs.CurrentColumn, &cs.CurrentIndex)
            if doUnpin then
                cs.StartingIndex <- cs.CurrentIndex
        else
            failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, the characters before the span can be marked to be released from memory.
    [<CompiledName("Advance")>]
    let advance doUnpin (cs: CharStream) span =
        if cs.CurrentIndex = span.IndexFrom then
            let characterCount = span.IndexTo - span.IndexFrom |> int
            for _i = 0 to characterCount do
                advanceByOne doUnpin cs
        else
            failwithf "Trying to consume the character span %O, from a stream that was left at %d." span cs.CurrentIndex

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    [<CompiledName("UnpinSpanAndGenerate")>]
    let unpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs ({IndexFrom = idxStart; IndexTo = idxEnd} as charSpan) =
        if cs.StartingIndex <= idxStart && cs.Source.LengthSoFar > idxEnd then
            cs.StartingIndex <- idxEnd + 1UL
            let length = idxEnd - idxStart + 1UL |> int
            let span = cs.Source.GetSpanForCharacters(idxStart, length)
            cs._LastUnpinnedSpanPosition <- charSpan.GetStartingPosition()
            fPostProcess.Invoke(symbol, cs._LastUnpinnedSpanPosition, span)
        else
            failwithf "Trying to read the character span %O, from a stream that was last read at %d." charSpan cs.StartingIndex

    /// Creates a string out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    /// It is recommended to use the `unpinSpanAndGenerate` function to avoid excessive allocations, unless you specifically want a string.
    [<CompiledName("UnpinSpanAndGenerateString")>]
    let unpinSpanAndGenerateString =
        let csCallback = CharStreamCallback (fun _ _ data -> box <| data.ToString())
        fun cs c_span ->
            let s =
                unpinSpanAndGenerate
                    null
                    csCallback
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
