// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open System
open System.Runtime.CompilerServices

/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
[<Struct; IsReadOnly>]
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
    /// Advances the position by one character and returns it.
    member x.Advance c =
        let struct (line, column) =
            match c with
            | '\n' ->
                x.Line + 1UL, 1UL
            | '\r' ->
                x.Line, x.Column
            | _ ->
                x.Line, x.Column + 1UL
        let index = x.Index + 1UL
        Position.Create line column index
    /// Advances the position by a read-only span of characters and returns it.
    member x.Advance(span: ReadOnlySpan<_>) =
        let mutable span = span
        let mutable line = x.Line
        let mutable column = x.Column
        let index = x.Index + uint64 span.Length
        while not span.IsEmpty do
            match span.IndexOfAny('\n', '\r') with
            | -1 ->
                column <- column + uint64 span.Length
                span <- ReadOnlySpan.Empty
            | nlPos ->
                if span.[nlPos] = '\n' then
                    line <- line + 1UL
                    column <- 1UL
                span <- span.Slice(nlPos + 1)
        Position.Create line column index

    /// A `Position` that points to the start of the text.
    static member Initial = {Line = 1UL; Column = 1UL; Index = 0UL}

    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column
