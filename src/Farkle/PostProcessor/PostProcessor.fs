// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Parser
open Farkle.PostProcessor

type PostProcessor<'TSymbol, 'TProduction> = internal {
    TerminalPostProcessor: TerminalPostProcessor<'TSymbol>
    ProductionPostProcessor: ProductionPostProcessor<'TProduction>
}
with
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

module PostProcessor =

    let create transformers fusers = either {
        let tpp = TerminalPostProcessor.create transformers
        let! ppp = ProductionPostProcessor.create fusers
        return {TerminalPostProcessor = tpp; ProductionPostProcessor = ppp}
    }