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
    /// Transforms a string to an arbotrary object.
    static member Transform x {TheTransformer = trans} = trans x

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that applies the given function to the symbol's data.
    let inline create sym (fTransform: _ -> 'TOutput) =
        {
            SymbolIndex = uint32 sym
            OutputType = typeof<'TOutput>
            TheTransformer = fTransform >> box
        }
