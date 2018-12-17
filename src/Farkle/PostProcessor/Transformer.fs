// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System
open Farkle
open Farkle.Collections

/// A delegate that accepts a `ReadOnlySpan` of characters and transforms it into an arbitrary object.
/// The word `C` means "Callback" and was shortened to avoid clutter in user code.
type C<'a> = delegate of ReadOnlySpan<char> -> 'a

/// A position-sensitive version of `C`.
type C2<'a> = delegate of Position * ReadOnlySpan<char> -> 'a

/// This type contains the logic to transform _one_ terminal symbol to an arbitrary object.
type Transformer = internal {
    SymbolIndex: uint32
    OutputType: Type
    TheTransformer: C2<obj>
}
with
    /// Creates a `Transformer` that transforms the `Terminal` with the given index
    static member Create idx (fTransformer: C2<'TOutput>) =
        {
            SymbolIndex = idx
            OutputType = typeof<'TOutput>
            TheTransformer = C2(fun pos data -> fTransformer.Invoke(pos, data) |> box)
        }

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that applies the given delegate to the symbol's data.
    let inline create sym (fTransform: C<'TOutput>) =
        Transformer.Create
            (uint32 sym)
            (C2(fun _ data -> fTransform.Invoke(data) |> box))

    /// Creates a `Transformer` that applies the given delegate to the symbol's data and position.
    let inline createPositionSensitive sym (fTransform: C2<'TOutput>) =
        Transformer.Create
            (uint32 sym)
            fTransform
