// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open System
open System.Runtime.CompilerServices

/// A point in 2D space with integer coordinates,
/// suitable for the position of a character in a text.
// The coordinates' representation is zero-based (the struct's
// default value is the initial position), but are exposed as
// one-based for compatibility and usability.
[<Struct; IsReadOnly>]
type Position private(line0: int, column0: int) =
    static let rec advanceImpl line column (span: ReadOnlySpan<_>) =
        match span.IndexOfAny('\n', '\r') with
        | -1 ->
            Position(line, column + span.Length)
        | nlPos ->
            let newSpan = span.Slice(nlPos + 1)
            if span.[nlPos] = '\n' || (span.[nlPos] = '\r' && nlPos < span.Length - 1 && span.[nlPos + 1] <> '\n') then
                advanceImpl (line + 1) 0 newSpan
            else
                advanceImpl line column newSpan
    /// The position's line. Numbering starts from 1.
    member _.Line = line0 + 1
    /// The position's column. Numbering starts from 1.
    member _.Column = column0 + 1
    override _.ToString() = $"({line0 + 1}, {column0 + 1})"
    /// Creates a position from zero-based coordinates.
    static member Create0 line column = Position(line, column)
    /// Creates a position from one-based coordinates.
    static member Create1 line column = Position(line - 1, column - 1)
    member internal _.NextLine() = Position(line0 + 1, 0)
    /// Advances the position by a read-only span of characters and returns it.
    member internal x.Advance(span: ReadOnlySpan<_>) =
        advanceImpl line0 column0 span
    /// A `Position` that points to the start of the text.
    static member Initial = Unchecked.defaultof<Position>

[<Struct; NoComparison; NoEquality>]
// Holds a position that can be advanced according to given characters.
// This is a mutable struct to properly support CR line endings.
// A problem of it is that it won't detect them in the end of input
// because CharStream type does not _yet_ have an innate concept of
// ending input. TODO: fix it.
type internal PositionTracker = struct
    val mutable private pos : Position
    val mutable private lastSeenCr: bool

    new(pos) = {pos = pos; lastSeenCr = false}
    // The current position.
    member x.Position = x.pos
    // Gets the position after the following characters,
    // starting from the tracker's current position.
    // This method does not modify the tracker.
    member x.GetPositionAfter(span: ReadOnlySpan<_>) =
        if span.IsEmpty then
            x.pos
        elif x.lastSeenCr && span.[0] <> '\n' then
            x.pos.NextLine().Advance(span)
        else
            x.pos.Advance(span)
    // Advances the tracker's current position by the given characters.
    member x.Advance(span: ReadOnlySpan<_>) =
        if not span.IsEmpty then
            x.pos <- x.GetPositionAfter span
            x.lastSeenCr <- span.[span.Length - 1] = '\r'
end
