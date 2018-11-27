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
    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column

/// Functions to work with the `Position` type.
module Position =

    open LanguagePrimitives

    /// Changes the position according to the given character.
    /// It always increases the index by one, but it takes newlines into account to
    /// change the line and column accordingly.
    let advance c {Line = line; Column = col; Index = idx} =
        let mkPos line col = {Line = line; Column = col; Index = idx + GenericOne}
        match c, col with
        | '\n', col when col = GenericOne -> mkPos line col
        | '\n', _ | '\r', _ -> mkPos (line + GenericOne) GenericOne
        | _ -> mkPos line (col + GenericOne)

    /// A `Position` that points to the beginning of a stream.
    let initial = {Line = GenericOne; Column = GenericOne; Index = GenericZero}