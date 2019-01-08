// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System
open Farkle

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
    /// Creates a `Transformer` that transforms the `Terminal`s with the
    /// given integer index in the grammar according to the given delegate.
    static member Create idx (fTransformer: C2<'TOutput>) =
        {
            SymbolIndex = idx
            OutputType = typeof<'TOutput>
            TheTransformer = C2(fun pos data -> fTransformer.Invoke(pos, data) |> box)
        }

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that applies the given delegate to the terminal's data and position.
    let inline createP sym fTransform = Transformer.Create (uint32 sym) fTransform

    /// Creates a `Transformer` that applies the given delegate to the terminal's data.
    let inline create sym (fTransform: C<_>) = createP sym (C2(fun _ data -> fTransform.Invoke(data)))

    /// Creates a `Transformer` that applies the given function to the terminal's data as a string, and its position.
    /// This function will cause an allocation of a string, so if you don't want the string itself
    /// (or its entirety), it is better to use the delegate-accepting variants.
    let inline createPS sym fTransform = createP sym (C2(fun pos data -> fTransform pos <| data.ToString()))

    /// Creates a `Transformer` that applies the given function to the terminal's data as a string.
    /// This function will cause an allocation of a string, so if you don't want the string itself
    /// (or its entirety), it is better to use the delegate-accepting variants.
    let inline createS sym fTransform = createPS sym (fun _ data -> fTransform data)

    /// Creates a `Transformer` that transforms a terminal's data to a 32-bit signed integer.
    let inline int sym =
        #if NETCOREAPP2_1
        create sym <| C Int32.Parse
        #else
        create sym <| C (fun data -> Int32.Parse(data.ToString()))
        #endif

    /// Creates a `Transformer` that transforms a terminal's data to a string as-is.
    let inline string sym = createS sym id

    /// Creates a `Transformer` that ignore's a terminal's data.
    /// It is better not to include a `Transformer` in a post-processor
    /// configuration, instead of including an ignoring one.
    let inline ignore sym = create sym <| C (fun _ -> null)
