// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Parser

/// A post-processor.
/// Post-processors convert `AST`s into some more meaningful types for the library that uses the parser.
type PostProcessor<'TSymbol, 'TProduction> = internal {
    TerminalPostProcessor: Map<uint32, Transformer>
    ProductionPostProcessor: Map<uint32, Fuser>
}
with
    /// Converts an `AST` to an arbitrary object, based on the post-processor in question.
    member this.PostProcessAST ast =
        let rec impl ast =
            match ast with
            | Content (sym, data) ->
                this.TerminalPostProcessor.TryFind sym
                |> Option.defaultValue Transformer.ignore
                |> Transformer.Transform data
                |> Ok
            | Nonterminal (prod, data) ->
                data
                |> List.map impl
                |> collect
                >>= (fun x -> this.ProductionPostProcessor.TryFind prod |> failIfNone (UnknownProduction <| prod.ToString()) >>= (fun f -> Fuser.Fuse x f))
                // >>= x.ProductionPostProcessor.PostProcess prod
        impl ast

/// Functions to create `PostProcessor`s.
module PostProcessor =

    /// Creates a `PostProcessor` from the given `TerminalPostProcessor` and `ProductionPostProcessor`.
    let create tpp ppp = {TerminalPostProcessor = tpp; ProductionPostProcessor = ppp}

    /// Creates a `PostProcessor` from the given sequences of symbols and `Transformer`s, and productions and `Fuser`s.
    let ofSeq transformers fusers =
        let tpp = Map.ofSeq transformers
        let ppp = Map.ofSeq fusers
        create tpp ppp
