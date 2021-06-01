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
    /// Advances the position by one character and returns it.
    /// This method does not correctly handle Windows line endings
    /// so it is recommended to use the overload that accepts a
    /// read-only span of characters instead.
    member x.Advance c =
        let struct (line, column) =
            match c with
            | '\r' | '\n' ->
                x.Line + 1UL, 1UL
            | _ ->
                x.Line, x.Column + 1UL
        let index = x.Index + 1UL
        Position.Create line column index
    /// Advances the position by a read-only span of characters and returns it.
    /// Both Windows line ending characters (carriage return and line feed) must
    /// be passed in the same span, othwerise the returned position will be incorrect.
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
                line <- line + 1UL
                column <- 1UL
                let nlCharactersToSkip =
                    if nlPos < span.Length - 1 && span.[nlPos] = '\r' && span.[nlPos + 1] = '\n' then
                        2
                    else
                        1
                span <- span.Slice(nlPos + nlCharactersToSkip)
        Position.Create line column index

    /// A `Position` that points to the start of the text.
    static member Initial = {Line = 1UL; Column = 1UL; Index = 0UL}

    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column
