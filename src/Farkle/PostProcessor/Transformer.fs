// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System

/// This type contains the logic to transform _one_ terminal symbol to an arbitrary object.
type Transformer = internal {
    SymbolIndex: uint32
    OutputType: Type
    TheTransformer: string -> obj
}
with
    static member Create idx output fTransformer = {SymbolIndex = idx; OutputType = output; TheTransformer = fTransformer}

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that applies the given function to the symbol's data.
    let inline create sym (fTransform: _ -> 'TOutput) =
        Transformer.Create
            (uint32 sym)
            typeof<'TOutput>
            (fTransform >> box)
