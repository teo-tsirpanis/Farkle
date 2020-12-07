// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open System
open System.Runtime.CompilerServices

[<Struct; IsReadOnly>]
/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
type Position = {
    /// The position's line.
    /// Numbering starts from 1.
    Line: uint64
    /// The position's column.
    /// Numbering starts from 1.
    Column: uint64
    /// The position's character index.
    /// Numbering starts from 0.
    Index: uint64
}
with
    static member Create line column index =
        {Line = line; Column = column; Index = index}
    /// Changes the line, column and character index references according to the given character.
    static member inline private AdvanceImpl (c, line: byref<_>, column: byref<_>, index: byref<_>) =
        index <- index + 1UL
        match c with
        | '\n' when column = 1UL -> ()
        | '\r' | '\n' ->
            line <- line + 1UL
            column <- 1UL
        | _ -> column <- column + 1UL

    /// Returns the position of the next character if it was the given one.
    member x.Advance c =
        let mutable line = x.Line
        let mutable column = x.Column
        let mutable index = x.Index
        Position.AdvanceImpl(c, &line, &column, &index)
        Position.Create line column index
    /// Applies `Position.Advance` successively for every character of the span.
    // No reason to make it public; this is an implementation detail.
    member internal x.Advance(span: ReadOnlySpan<_>) =
        if span.Length = 0 then
            x
        else
            let mutable line = x.Line
            let mutable column = x.Column
            let mutable index = x.Index
            for i = 0 to span.Length - 1 do
                Position.AdvanceImpl(span.[i], &line, &column, &index)
            Position.Create line column index

    /// A `Position` that points to the start.
    static member Initial = {Line = 1UL; Column = 1UL; Index = 0UL}

    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column
