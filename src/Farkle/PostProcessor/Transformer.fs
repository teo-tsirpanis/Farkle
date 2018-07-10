// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open System

/// This type contains the logic to transform _one_ terminal symbol to an arbitrary object.
type Transformer<'TSymbol> = internal {
    AcceptingSymbol: 'TSymbol
    OutputType: Type
    TheTransformer: string -> obj
}
with
    /// Transforms a string to an arbotrary object.
    static member Transform x {TheTransformer = trans} = trans x

/// Functions to create `Transformer`s.
module Transformer =

    /// Creates a `Transformer` that transforms the symbol of the given type.
    let create (sym: 'TSymbol) (fTransform: _ -> 'TOutput) =
        {
            AcceptingSymbol = sym
            OutputType = typeof<'TOutput>
            TheTransformer = fTransform >> box
        }

    let internal unknownTerminal x = create x UnknownTerminal

/// This type contains the logic to transform _all_ terminal symbols of a grammar to arbitrary objects.
// I can't use a map, because the compiler starts a "not-so-generic-code" rant.
type TerminalPostProcessor<'TSymbol> = internal TerminalPostProcessor of ('TSymbol -> Transformer<'TSymbol>)
with
    /// Transforms a string of the specified symbol to an arbitrary object.
    /// In case it cannot transform it, it will transform it to an object of type `UnknownTerminal`.
    member x.PostProcess sym data =
        x
        |> (fun (TerminalPostProcessor x) -> x sym)
        |> (Transformer<'TSymbol>.Transform data)

/// Functions to create `TerminalPostProcessor`s.
module TerminalPostProcessor =

    /// Creates a `TerminalPostProcessor` that transforms all
    /// the symbols that are recognized by the given `Transformer`s.
    /// In case many transformers recognize one symbol, the last one in order will be considered.
    let create transformers =
        let map = transformers |> Seq.map (fun x -> x.AcceptingSymbol, x) |> Map.ofSeq
        (fun x -> map.TryFind x |> Option.defaultValue (Transformer.unknownTerminal x)) |> TerminalPostProcessor