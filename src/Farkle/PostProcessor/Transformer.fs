// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System

/// This type contains the logic to transform _one_ terminal symbol to an arbitrary object.
type Transformer = internal {
    OutputType: Type
    TheTransformer: string -> obj
}
with
    /// Transforms a string to an arbotrary object.
    static member Transform x {TheTransformer = trans} = trans x

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that applies the given function to the symbol's data.
    let create (fTransform: _ -> 'TOutput) =
        {
            OutputType = typeof<'TOutput>
            TheTransformer = fTransform >> box
        }

    /// A `Transformer` that ignores the symbol's data.
    let ignore = create UnknownTerminal
