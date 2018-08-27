// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar

/// A post-processor.
/// Post-processors convert `AST`s into some more meaningful types for the library that uses the parser.
type PostProcessor = internal {
    TerminalPostProcessor: Map<uint32, Transformer>
    ProductionPostProcessor: Map<uint32, Fuser>
}

/// Functions to create `PostProcessor`s.
module PostProcessor =

    /// Creates a `PostProcessor` from the given `TerminalPostProcessor` and `ProductionPostProcessor`.
    let internal create tpp ppp = {TerminalPostProcessor = tpp; ProductionPostProcessor = ppp}

    /// Creates a `PostProcessor` from the given sequences of symbols and `Transformer`s, and productions and `Fuser`s.
    let ofSeq transformers fusers =
        let tpp = Map.ofSeq transformers
        let ppp = Map.ofSeq fusers
        create tpp ppp

    /// Creates a `PostProcessor` from the given sequences whose first tuple members can be converted to unsigned 32-bit integers.
    let inline ofSeqEnum transformers fusers =
        let tpp = transformers |> Seq.map (fun (sym, trans) -> uint32 sym, trans)
        let ppp = fusers |> Seq.map (fun (prod, fus) -> uint32 prod, fus)
        ofSeq tpp ppp

    /// Converts an `AST` to an arbitrary object, based on the post-processor in question.
    let postProcessAST {TerminalPostProcessor = tpp; ProductionPostProcessor = ppp} ast =
        let rec impl ast =
            match ast with
            | AST.Content tok ->
                tok.Symbol
                |> Symbol.tryGetTerminalIndex
                |> Option.bind tpp.TryFind
                |> Option.defaultValue Transformer.ignore
                |> Transformer.Transform tok.Data
                |> Ok
            | AST.Nonterminal (prod, data) ->
                data
                |> List.map impl
                |> collect
                >>= (fun x -> ppp.TryFind prod.Index |> failIfNone (UnknownProduction <| prod.ToString()) >>= (fun f -> Fuser.Fuse x f))
        impl ast
