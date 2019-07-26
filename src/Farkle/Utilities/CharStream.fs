// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open LanguagePrimitives
open Operators.Checked
open System
open System.IO

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

/// The internal structure to support `CharStreamSource.DynamicBlock`.
type private DynamicBlock =
    {
        /// The `TextReader` that powers the stream.
        Reader: TextReader
        /// A character array where the characters that get read, but are still needed are stored.
        mutable Buffer: char[]
        /// The index of the first element in the buffer.
        mutable BufferStartingIndex: uint64
        /// The index of the next character to be read.
        mutable NextReadIndex: uint64
    }
    /// The real length of the buffer, excluding the unpinned characters at the end.
    member x.BufferContentLength = x.NextReadIndex - x.BufferStartingIndex |> int
    interface IDisposable with
        member x.Dispose() =
            x.Reader.Dispose()
            GC.SuppressFinalize(x)
    override x.Finalize() = (x :> IDisposable).Dispose()

/// A representation of a `CharStream`.
type private CharStreamSource =
    /// A representation of a `CharStream` that stores
    /// the characters in one continuous area of memory.
    /// It is not recommended for large files.
    | StaticBlock of ReadOnlyMemory<char>
    /// A representation of a `CharStream` that lazily
    /// loads input from a `TextReader` when it is needed
    /// and unloads it when it is not.
    | DynamicBlock of DynamicBlock
    /// Gets a specified character by index; if it exists in memory, or raises an exception.
    member x.Item
        with get idx =
            match x with
            | StaticBlock sb -> sb.Span.[int idx]
            | DynamicBlock db ->
                if idx >= db.BufferStartingIndex && idx < db.NextReadIndex then
                    let position = int <| idx - db.BufferStartingIndex
                    db.Buffer.[position]
                else
                    failwithf "Trying to get character at %d, while only characters from %d to %d have been loaded"
                        idx db.BufferStartingIndex <| db.NextReadIndex - 1UL
    /// Returns how many characters have been read so far.
    /// In dynamic block streams, it doesn't mean that all these characters are still on memory.
    member x.LengthSoFar =
        match x with
        | StaticBlock sb -> uint64 sb.Length
        | DynamicBlock db -> db.NextReadIndex
    interface IDisposable with
        member x.Dispose() =
            match x with
            | StaticBlock _ -> ()
            | DynamicBlock db -> (db :> IDisposable).Dispose()

/// A data structure that supports efficient and copy-free access to a read-only sequence of characters.
/// It is not thread-safe.
/// Also, if you create a character stream from a `TextReader`, you must dispose it afterwards.
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
    /// Gets the stream's current position.
    /// Reading the stream will start from here.
    member x.GetCurrentPosition() = {Line = x.CurrentLine; Column = x.CurrentColumn; Index = x.CurrentIndex}
    /// The starting position of the last character span that was unpinned.
    member x.LastUnpinnedSpanPosition = x._LastUnpinnedSpanPosition
    /// The stream's character at its current position.
    /// Calling this function assumes that this character is actually `read`.
    member x.FirstCharacter = x.Source.[x.CurrentIndex]
    /// Returns the length of the input; or at least the
    /// length of the input that has ever crossed the memory.
    member x.LengthSoFar = x.Source.LengthSoFar
    interface IDisposable with
        member x.Dispose() = (x.Source :> IDisposable).Dispose()

/// A type pointing to a character in a character stream.
type [<Struct>] CharStreamIndex = private CharStreamIndex of uint64
with
    /// The zero-based index this object points to, starting from the beginning of the stream.
    member x.Index = match x with CharStreamIndex idx -> idx

/// A .NET delegate that is the interface between the `CharStream` API and the post-processor.
/// It accepts a generic type (a `Terminal` usually), the `Position` of the symbol, and a
/// `ReadOnlySpan` of characters that are going to be converted into an object.
/// This type is not an F# native function type, because of limitations while handling `ReadOnlySpan`s.
type CharStreamCallback<'symbol> = delegate of 'symbol * Position * ReadOnlySpan<char> -> obj

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module CharStream =

    /// Creates a `CharStreamIndex` from a `CharStream` that points to its current position.
    let getCurrentIndex (cs: CharStream) = CharStreamIndex cs.CurrentIndex

    /// Reads the `idx`th character of `cs`, places it in `c` and returns `true`, if there are more characters left to be read.
    /// Otherwise, returns `false`.
    let readChar cs (c: outref<_>) (idx: byref<CharStreamIndex>) =
        match cs.Source with
        | _ when idx.Index < cs.CurrentIndex ->
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been consumed." idx.Index cs.CurrentIndex
        | StaticBlock sb when idx.Index < uint64 sb.Length ->
            c <- sb.Span.[int idx.Index]
            idx <- idx.Index + GenericOne |> CharStreamIndex
            true
        | StaticBlock _ -> false
        | DynamicBlock db ->
            // The character we want to read is already in memory. Easy stuff.
            if idx.Index < db.NextReadIndex then
                c <- cs.Source.[idx.Index]
                idx <- CharStreamIndex <| idx.Index + GenericOne
                true
            // The character we want to read is the next one to be read.
            elif idx.Index = db.NextReadIndex then
                // The buffer might be full however.
                if db.BufferContentLength = db.Buffer.Length then
                    // If not all characters in the buffer are needed, we move those we need to the start.
                    if db.BufferStartingIndex <> cs.StartingIndex then
                        let importantCharStart = int <| cs.StartingIndex - db.BufferStartingIndex
                        let importantCharLength = int <| db.NextReadIndex - cs.StartingIndex
                        Array.blit db.Buffer importantCharStart db.Buffer 0 importantCharLength
                        db.BufferStartingIndex <- cs.StartingIndex
                    // Otherwise, we double the buffer's size. It is doubled to achieve amortized constant complexity.
                    // But to reach the point of growing it, many times, we must have huge terminals. The default
                    // buffer size is some hundreds of characters.
                    else
                        Array.Resize(&db.Buffer, db.Buffer.Length * 2)
                // It's now time to read the next character.
                // We read each character at a time from the reader. It does not adversely
                // affect performance because the reader has its own buffer as well!
                let cRead = db.Reader.Read()
                // There are still characters to read. We store this little
                // character in the buffer, and return it as well.
                if cRead <> -1 then
                    db.NextReadIndex <- db.NextReadIndex + 1UL
                    db.Buffer.[int <| idx.Index - db.BufferStartingIndex] <- char cRead
                    c <- char cRead
                    idx <- CharStreamIndex <| db.NextReadIndex
                    true
                // We have reached the end.
                else
                    false
            // We cannot read past the first character that has not been read.
            // This is how the CharStream works; you have to read one character at a time.
            else
                failwithf "Cannot read character at %d because the latest one was read at %d." idx.Index db.NextReadIndex

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    let pinSpan (cs: CharStream) (CharStreamIndex idxTo) = {
        LineFrom = cs.CurrentLine
        ColumnFrom = cs.CurrentColumn
        IndexFrom = cs.CurrentIndex
        IndexTo = idxTo
    }

    /// Creates a new `CharSpan` that spans one character more than the given one.
    /// A `CharStream` must also be given to validate its length so that out-of-bounds errors are prevented.
    let extendSpanByOne (cs: CharStream) span =
        if span.IndexTo < cs.LengthSoFar then
            {span with IndexTo = span.IndexTo + 1UL}
        else
            failwith "Trying to extend a character span by one character past these that were already read."

    /// Creates a new `CharSpan` from the union of two adjacent spans, i.e.
    /// that starts at the first's start, and ends at the second's end.
    let concatSpans span1 span2 =
        if span1.IndexTo = span2.IndexFrom then
            {span1 with IndexTo = span2.IndexTo}
        else
            failwithf "Trying to concatenate character span %O with %O." span1 span2

    /// Advances a `CharStream`'s position by one character.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character and all before it can be marked to be released from memory.
    let consumeOne doUnpin (cs: CharStream) =
        if cs.CurrentIndex < uint64 cs.LengthSoFar then
            Position.AdvanceImpl(cs.FirstCharacter, &cs.CurrentLine, &cs.CurrentColumn, &cs.CurrentIndex)
            if doUnpin then
                cs.StartingIndex <- cs.CurrentIndex
        else
            failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, the characters before the span can be marked to be released from memory.
    let consume doUnpin (cs: CharStream) span =
        if cs.CurrentIndex = span.IndexFrom then
            for _i = int span.IndexFrom to int span.IndexTo do
                consumeOne doUnpin cs
        else
            failwithf "Trying to consume the character span %O, from a stream that was left at %d." span cs.CurrentIndex

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    let unpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs ({IndexFrom = idxStart; IndexTo = idxEnd} as span) =
        if cs.StartingIndex <= idxStart && cs.LengthSoFar > idxEnd then
            cs.StartingIndex <- idxEnd + 1UL
            let length = idxEnd - idxStart + 1UL |> int
            let span =
                match cs.Source with
                | StaticBlock sb -> sb.Span.Slice(int idxStart, length)
                | DynamicBlock db -> ReadOnlySpan(db.Buffer).Slice(int <| idxStart - db.BufferStartingIndex, length)
            cs._LastUnpinnedSpanPosition <- cs.GetCurrentPosition()
            fPostProcess.Invoke(symbol, cs.LastUnpinnedSpanPosition, span)
        else
            failwithf "Trying to read the character span %O, from a stream that was last read at %d." span cs.StartingIndex

    /// Creates a string out of the characters at the given `CharSpan`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    /// It is recommended to use the `unpinSpanAndGenerate` function to avoid excessive allocations, unless you specifically want a string.
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

    let private create src =
        {
            Source = src
            StartingIndex = 0UL
            CurrentLine = Position.Initial.Line
            CurrentColumn = Position.Initial.Column
            CurrentIndex = Position.Initial.Index
            _LastUnpinnedSpanPosition = Position.Initial
        }

    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    let ofReadOnlyMemory mem = mem |> StaticBlock |> create

    /// Creates a `CharStream` from a string.
    let ofString (x: string) = x.AsMemory() |> ofReadOnlyMemory

    [<Literal>]
    let private defaultBufferSize = 256

    /// Creates a `CharStream` that lazily reads from a `TextReader`.
    /// The size of the stream's internal character buffer is specified.
    /// This buffer holds the characters that are the data for a terminal under discovery.
    /// If a terminal is longer than the buffer's size, the buffer becomes twice as long each time.
    /// The default buffer size is 256 characters. If the specified size is not positive, the default is used.
    /// Also, the character stream must be disposed afterwards.
    let ofTextReaderEx bufferSize textReader =
        let buffer =
            if bufferSize > 0 then
                bufferSize
            else
                defaultBufferSize
            |> Array.zeroCreate
        {
            Reader = textReader
            Buffer = buffer
            BufferStartingIndex = 0UL
            NextReadIndex = 0UL
        } |> DynamicBlock |> create

    /// Creates a `CharStream` from a `TextReader`.
    /// It must be disposed afterwards.
    let ofTextReader textReader = ofTextReaderEx defaultBufferSize textReader
