// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open System

/// An continuous range of characters that is
/// stored by its starting index and length.
type CharSpan = private CharSpan of start: uint64 * length: int

/// A representation of a `CharStream` that stores
/// the characters in one continuous area of memory.
/// It is not recommended for large files.
type private StaticBlock = {
    Stream: ReadOnlyMemory<char>
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

/// A type-checked character index of a `CharStream`.
type CharStreamIndex = private CharStreamIndex of uint64
with
    /// The index's actual, numeric index.
    member x.Index = match x with | CharStreamIndex x -> x

/// A type that gives a `CharStream` an F# `list`-like interface.
type CharStreamView = private CharStreamView of stream: CharStream * idx: CharStreamIndex
with
    /// The character index the view is in, starting from the beginning of the stream.
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
        | StaticBlock sb -> CharStreamView (cs, CharStreamIndex sb.Position.Index)

    /// An active pattern to access a `CharStreamView` as if it was an F# `list`.
    let (|CSCons|CSNil|) (CharStreamView (cs, CharStreamIndex idx)) =
        match cs with
        | StaticBlock sb when idx < uint64 sb.Stream.Length ->
            CSCons(sb.Stream.Span.[int idx], CharStreamView(cs, CharStreamIndex <| idx + GenericOne))
        | StaticBlock _ -> CSNil

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    let pinSpan cs (CharStreamIndex idxTo) =
        match cs with
        | StaticBlock {Stream = b; Position = {Index = idxFrom}} -> CharSpan (idxFrom, int <| idxTo - idxFrom)

    /// Creates a new `CharSpan` from two continuous spans, i.e. the first one ends when the second one starts.
    /// Returns `None` if they were not continuous.
    let tryAppendSpans (CharSpan (start1, length1)) (CharSpan (start2, length2)) =
        if start1 + uint64 length1 = start2 then
            Some <| CharSpan (start1, length1 + length2)
        else
            None

    /// Advances the `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    let consume cs (CharSpan (start, length)) =
        match cs with
        | StaticBlock sb when sb.Position.Index = start ->
            let span = sb.Stream.Span.Slice(int start, length)
            for i = 0 to span.Length - 1
                do sb.Position <- Position.advance span.[i] sb.Position
        | StaticBlock _ -> ()

    /// Creates an arbitrary object out of the characters at the given `CharSpan`.
    /// After that call, these characters might be freed from memory, so this function should not be used twice.
    let tryUnpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs (CharSpan (start, length)) =
        match cs with
        | StaticBlock sb when uint64 sb.Stream.Length > start + uint64 length ->
            let span = sb.Stream.Span.Slice(int start, length)
            fPostProcess.Invoke(symbol, sb.Position, span) |> Some
        | StaticBlock _ -> None
