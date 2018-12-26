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

/// A representation of a `CharStream`.
type private CharStreamSource =
    /// A representation of a `CharStream` that stores
    /// the characters in one continuous area of memory.
    /// It is not recommended for large files.
    | StaticBlock of ReadOnlyMemory<char>

/// A data structure that supports efficient and copy-free access to a read-only sequence of characters.
/// It is not thread-safe.
type CharStream = private {
    Source: CharStreamSource
    mutable StartingIndex: uint64
    mutable _Position: Position
}
with
    /// The stream's current position.
    member x.Position = x._Position
    /// The stream's character at its current position.
    member x.FirstCharacter = match x.Source with StaticBlock sb -> sb.Span.[int x.Position.Index]

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
    let view cs = CharStreamView (cs, cs.Position.Index)

    /// An active pattern to access a `CharStreamView` as if it was an F# `list`.
    let (|CSCons|CSNil|) (CharStreamView (cs, idx)) =
        match cs.Source with
        | StaticBlock sb when idx < cs.StartingIndex ->
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been read." idx cs.StartingIndex
        | StaticBlock sb when idx < uint64 sb.Length ->
            CSCons(sb.Span.[int idx], CharStreamView(cs, idx + GenericOne))
        | StaticBlock _ -> CSNil

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    let pinSpan (CharStreamView(cs, idxTo)) =
        match cs.Source with
        | StaticBlock _ -> CharSpan (cs.Position.Index, idxTo)

    /// Creates a new `CharSpan` that spans one character more than the given one.
    /// A `CharStream` must also be given to validate its length so that out-of-bounds errors are prevented.
    let extendSpanByOne cs (CharSpan (idxStart, idxEnd)) =
        match cs.Source with
        | StaticBlock sb when idxEnd < uint64 sb.Length -> CharSpan (idxStart, idxEnd + GenericOne)
        | StaticBlock _ -> failwith "Trying to extend a character span by one character past its end."

    /// Creates a new `CharSpan` from two continuous spans, i.e. that starts at the first's start, and ends at the second's end.
    /// Returns `None` if they were not continuous.
    let extendSpans (CharSpan (start1, end1)) (CharSpan (start2, end2)) =
        if end1 = start2 then
            CharSpan (start1, end2)
        else
            failwithf "Trying to extend a character span that ends at %d, with one that starts at %d." end1 start2

    /// Advances a `CharStream`'s position by one character.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character can be marked to be released from memory.
    let consumeOne doUnpin cs =
        match cs.Source with
        | StaticBlock sb when cs.Position.Index < uint64 sb.Length ->
            if doUnpin then
                if cs.StartingIndex = cs.Position.Index then
                    cs.StartingIndex <- cs.StartingIndex + 1UL
                else
                    failwithf "Cannot unpin a character from a stream with current and starting index being %d and %d (not equal)." cs.Position.Index cs.StartingIndex
            cs._Position <- Position.advance sb.Span.[int cs.Position.Index] cs._Position
        | StaticBlock _ -> failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, these characters can be marked to be released from memory.
    let consume doUnpin (cs: CharStream) (CharSpan (idxStart, idxEnd) as csp) =
        if cs.Position.Index = idxStart then
            for i = int idxStart to int idxEnd do
                consumeOne doUnpin cs
        else
            failwithf "Trying to consume the character span %O, from a stream that was left at %d." csp cs.Position.Index

    /// Creates an arbitrary object out of the characters at the given `CharSpan` at the returned `Position`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    let unpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs (CharSpan (idxStart, idxEnd) as csp) =
        match cs.Source with
        | StaticBlock sb when cs.StartingIndex = idxStart && uint64 sb.Length > idxEnd ->
            cs.StartingIndex <- idxEnd + 1UL
            let length = idxEnd - idxStart + 1UL |> int
            let span = sb.Span.Slice(int idxStart, length)
            fPostProcess.Invoke(symbol, cs.Position, span), cs.Position
        | StaticBlock _ ->
            failwithf "Trying to read the character span %O, from a stream that was last read at %d." csp cs.StartingIndex

    /// Creates a string out of the characters at the given `CharSpan` at the returned `Position`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    /// It is recommended to use the `unpinSpanAndGenerate` function to avoid excessive allocations, unless you specifically want a string.
    let unpinSpanAndGenerateString cs c_span =
        let (s, pos) =
            unpinSpanAndGenerate
                null
                (CharStreamCallback(fun _ _ data -> box <| data.ToString()))
                cs
                c_span // Created by cable
        s :?> string, pos

    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    let ofReadOnlyMemory mem = {Source = StaticBlock mem; StartingIndex = 0UL; _Position = Position.initial}

    let ofString (x: string) = x.AsMemory() |> ofReadOnlyMemory
