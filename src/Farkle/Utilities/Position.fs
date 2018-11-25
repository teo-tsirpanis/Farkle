// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// The zero-based index of a character in a stream.
type CharacterIndex = private CharacterIndex of uint64
with
    /// The index's value.
    member x.Index = match x with CharacterIndex x -> x

/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
type Position = private {
    _Line: uint64
    _Column: uint64
    _Index: CharacterIndex
}
with
    /// The position's line.
    member x.Line = x._Line
    /// The position's column.
    member x.Column = x._Column
    /// The position's character index.
    member x.Index = x._Index
    override x.ToString() =
        sprintf "(%d, %d)" x.Line x.Column

/// Functions to work with the `Position` type.
module Position =

    open LanguagePrimitives

    /// Changes the position according to the given character.
    /// It always increases the index by one, but it takes newlines into account to
    /// change the line and column accordingly.
    let advance c {_Line = line; _Column = col; _Index = CharacterIndex idx} =
        let mkPos line col = {_Line = line; _Column = col; _Index = CharacterIndex <| idx + GenericOne}
        match c, col with
        | '\n', col when col = GenericOne -> mkPos line col
        | '\n', _ | '\r', _ -> mkPos (line + GenericOne) GenericOne
        | _ -> mkPos line (col + GenericOne)

    /// A `Position` that points to the beginning of a stream.
    let initial = {_Line = GenericOne; _Column = GenericOne; _Index = CharacterIndex GenericZero}