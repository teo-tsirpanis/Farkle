// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open System

/// An continuous range of characters that is
/// stored by its starting index and length.
type CharSpan = private CharSpan of idxFrom: uint64 * idxTo: uint64
with
    /// The span's zero-based index of the first character.
    member x.StartingIndex = match x with | CharSpan (x, _) -> x
    /// The span's zero-based index of the last character.
    member x.EndingIndex = match x with | CharSpan (_, x) -> x
    override x.ToString() = sprintf "[%d,%d]" x.StartingIndex x.EndingIndex

/// A representation of a `CharStream` that stores
/// the characters in one continuous area of memory.
/// It is not recommended for large files.
type private StaticBlock = {
    Stream: ReadOnlyMemory<char>
    mutable StartingIndex: uint64
    mutable Position: Position
}

/// A data structure that supports efficient and copy-free access to a read-only sequence of characters.
/// It is not thread-safe.
type CharStream = private StaticBlock of StaticBlock
with
    /// The stream's current position.
    member x.Position = match x with StaticBlock sb -> sb.Position
    /// The stream's character at its current position.
    member x.FirstCharacter = match x with StaticBlock sb -> sb.Stream.Span.[int sb.Position.Index]

/// A type that gives a `CharStream` an F# `list`-like interface.
type CharStreamView = private CharStreamView of stream: CharStream * idx: uint64
with
    /// The zero-based index the view is in, starting from the beginning of the stream.
    member x.Index = match x with | CharStreamView (_, idx) -> idx

/// A .NET delegate that is the interface between the `CharStream` API and the post-processor.
/// It accepts a generic type (a `Symbol` usually), the `Position` of the symbol, and a
/// `ReadOnlySpan` of characters that are going to be converted into an object.
/// This type is not an F# native function type, because of limitations while handling `ReadOnlySpan`s.
type CharStreamCallback<'symbol> = delegate of 'symbol * Position * ReadOnlySpan<char> -> obj

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module CharStream =

    open LanguagePrimitives
    open Operators.Checked

    /// Creates a `CharStreamView` from a `CharStream`.
    let view cs =
        match cs with
        | StaticBlock sb -> CharStreamView (cs, sb.Position.Index)

    /// An active pattern to access a `CharStreamView` as if it was an F# `list`.
    let (|CSCons|CSNil|) (CharStreamView (cs, idx)) =
        match cs with
        | StaticBlock sb when idx < sb.StartingIndex ->
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been read." idx sb.StartingIndex
        | StaticBlock sb when idx < uint64 sb.Stream.Length ->
            CSCons(sb.Stream.Span.[int idx], CharStreamView(cs, idx + GenericOne))
        | StaticBlock _ -> CSNil

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    let pinSpan (CharStreamView(cs, idxTo)) =
        match cs with
        | StaticBlock {Stream = _; Position = {Index = idxFrom}} -> CharSpan (idxFrom, idxTo)

    /// Creates a new `CharSpan` that spans one character more than the given one.
    /// A `CharStream` must also be given to validate its length so that out-of-bounds errors are prevented.
    let extendSpanByOne cs (CharSpan (idxStart, idxEnd)) =
        match cs with
        | StaticBlock sb when idxEnd < uint64 sb.Stream.Length -> CharSpan (idxStart, idxEnd + GenericOne)
        | StaticBlock _ -> failwith "Trying to extend a character span by one character past its end."

    /// Creates a new `CharSpan` from two continuous spans, i.e. that starts at the first's start, and ends at the second's end.
    /// Returns `None` if they were not continuous.
    let extendSpans (CharSpan (start1, end1)) (CharSpan (start2, end2)) =
        if end1 = start2 then
            CharSpan (start1, end2)
        else
            failwithf "Trying to extend a character span that ends at %d, with one that starts at %d." end1 start2

    /// Advances a `CharStream`'s position by one character.
    let consumeOne cs =
        match cs with
        | StaticBlock sb when sb.Position.Index < uint64 sb.Stream.Length ->
            sb.Position <- Position.advance sb.Stream.Span.[int sb.Position.Index] sb.Position
        | StaticBlock _ -> failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    let consume cs (CharSpan (idxStart, idxEnd)) =
        match cs with
        | StaticBlock sb when sb.Position.Index = idxStart ->
            for i = int idxStart to int idxEnd do
                consumeOne cs
        | StaticBlock {Position = {Index = idxCurr}} ->
            failwithf "Trying to consume a character span [%d, %d], from a stream that was left at %d." idxStart idxEnd idxCurr

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, these characters might be freed from memory, so this function must not be used twice.
    let unpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs (CharSpan (idxStart, idxEnd)) =
        match cs with
        | StaticBlock sb when sb.StartingIndex = idxStart && uint64 sb.Stream.Length > idxEnd ->
            sb.StartingIndex <- idxEnd
            let length = idxEnd - idxStart + 1UL |> int
            let span = sb.Stream.Span.Slice(int idxStart, length)
            fPostProcess.Invoke(symbol, sb.Position, span)
        | StaticBlock _ -> failwithf "Error while unpinning the character span: Tried to"

    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    let ofReadOnlyMemory mem = StaticBlock {Stream = mem; StartingIndex = 0UL; Position = Position.initial}
