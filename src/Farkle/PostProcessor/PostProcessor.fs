// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar.GOLDParser

/// A post-processor.
/// Post-processors convert `AST`s into some more meaningful types for the library that uses the parser.
type PostProcessor<'a> =
    /// Converts a generic token into an arbitrary object.
    /// In case of an insignificant token, implementations can return a boxed `()`, or `null`.
    abstract Transform: Token -> obj
    /// Fuses the many members of a production into one object.
    /// Fusing production rules must always succeed. In very case of an error like
    /// an unrecognized production, the function must return `false`.
    abstract Fuse: uint32 * obj[] * outref<obj> -> bool

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

    // let typeCheck (grammar: GOLDGrammar) transformers fusers =
    //     let terminals = 

    // let ofSeq2<'result> transformers fusers =
    //     0
