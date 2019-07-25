// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
type Position = {
    /// The position's line.
    Line: uint64
    /// The position's column.
    Column: uint64
    /// The position's character index.
    Index: uint64
}
with
    /// Changes the line, column and character index references according to the given character.
    static member internal AdvanceImpl (c, line: byref<_>, column: byref<_>, index: byref<_>) =
        index <- index + 1UL
        match c with
        | '\n' when column = 1UL -> ()
        | '\r' | '\n' ->
            line <- line + 1UL
            column <- 1UL
        | _ -> column <- column + 1UL

    /// Changes the position according to the given character.
    /// It always increases the index by one, but it takes newlines into account to
    /// change the line and column accordingly.
    member x.Advance c =
        let mutable line = x.Line
        let mutable column = x.Column
        let mutable index = x.Index
        Position.AdvanceImpl(c, &line, &column, &index)
        {Line = line; Column = column; Index = index}

    /// A `Position` that points to the start.
    static member Initial = {Line = 1UL; Column = 1UL; Index = 0UL}

    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column
