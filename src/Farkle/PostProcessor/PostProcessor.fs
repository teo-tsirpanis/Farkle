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
    TerminalPostProcessor: TerminalPostProcessor<'TSymbol>
    ProductionPostProcessor: ProductionPostProcessor<'TProduction>
}
with
    /// Converts an `AST` to an arbitrary object, based on the post-processor in question.
    member x.PostProcessAST ast =
        let rec impl ast =
            match ast with
            | Content (sym, data) -> x.TerminalPostProcessor.PostProcess sym data |> Ok
            | Nonterminal (prod, data) ->
                data
                |> List.map impl
                |> collect
                >>= x.ProductionPostProcessor.PostProcess prod
        impl ast

/// Functions to create `PostProcessor`s.
module PostProcessor =

    /// Creates a `PostProcessor` from the given `TerminalPostProcessor` and `ProductionPostProcessor`.
    let create tpp ppp = {TerminalPostProcessor = tpp; ProductionPostProcessor = ppp}

    /// Creates a `PostProcessor` from the given sequences of symbols and `Transformer`s, and productions and `Fuser`s.
    let ofSeq transformers fusers =
        let tpp = TerminalPostProcessor.ofSeq transformers
        let ppp = ProductionPostProcessor.ofSeq fusers
        create tpp ppp

    /// Creates a `PostProcessor` from the given functions of enumeration types.
    let ofEnumFunc fTransformers fFusers =
        let tpp = TerminalPostProcessor.ofEnumFunc fTransformers
        let ppp = ProductionPostProcessor.ofEnumFunc fFusers
        create tpp ppp