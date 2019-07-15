// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System
open Farkle

/// <summary>A delegate that accepts a <see cref="ReadOnlySpan{Char}"/> and transforms it into an arbitrary object.</summary>
/// <remarks>
///     <para>In F#, this type is named <c>C</c> - from "Callback" and was shortened to avoid clutter in user code.</para>
///     <para>A .NET delegate was used because <c>ReadOnlySpan</c>s are incompatible with F# functions</para>
/// </remarks>
[<CompiledName("TransformerCallback`1")>]
type C<'a> = delegate of ReadOnlySpan<char> -> 'a

/// <summary>A position-sensitive version of <see cref="TransformerCallback{C}"/>.</summary>
/// <remarks>
///     <para>In F#, this type is named <c>C2</c> - from "Callback" and was shortened to avoid clutter in user code.</para>
///     <para>A .NET delegate was used because <c>ReadOnlySpan</c>s are incompatible with F# functions</para>
/// </remarks>
[<CompiledName("PositionedTransformerCallback`1")>]
type C2<'a> = delegate of Position * ReadOnlySpan<char> -> 'a

/// This type contains the logic to transform one terminal symbol to an arbitrary object.
[<CompiledName("FSharpTransformer")>]
type Transformer = internal Transformer of (uint32 * C2<obj>)
with
    /// <summary>Creates a <see cref="Transformer"/> that transforms the <see cref="Terminal"/> with the
    /// given integer index in the grammar, according to the given delegate.</summary>
    static member Create idx (fTransformer: C2<'TOutput>) =
        Transformer (idx, C2(fun pos data -> fTransformer.Invoke(pos, data) |> box))

/// Functions to create `Transformer`s.
[<CompiledName("FSharpTransformerModule")>]
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
