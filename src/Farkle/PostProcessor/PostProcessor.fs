// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar
open System.Collections.Generic

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

    let typeCheck<'result> transformers fusers =
        let terminalToType =
            transformers
            |> Seq.map (fun {SymbolIndex = sym; OutputType = t} -> sym, t)
            |> Map.ofSeq
        let allProductions =
            fusers
            |> Seq.groupBy (fun {Production = {Head = head}} -> head)
            |> Map.ofSeq
        let productionToType = Dictionary()
        let tryGet k =
            let mutable x = null
            if productionToType.TryGetValue(k, &x) then
                Some x
            else
                None
        let rec impl ({Head = head} as prod) typ =
            let matchSymbols {Production = {Handle = handle}; InputTypes = types} =
                let matchSymbol typ =
                    function
                    | Terminal (idx, _) when terminalToType.[idx] = typ -> Ok ()
                    | Terminal (idx, _) as sym -> Error <| sprintf "Type mismatch: Expected %O, but got %O for symbol %O" typ terminalToType.[idx] sym
                    | Nonterminal (idx, _) as sym -> Ok()
                if handle.Length = types.Length then
                    let mutable i = 0
                    let mutable x = Ok ()
                    while i < handle.Length && isOk x do
                        x <- matchSymbol types.[i] handle.[i]
                    x
                else
                    Error <| sprintf "The fuser of production %O accepts a different number of types" prod
            match tryGet prod with
            | Some t when t = typ -> Ok ()
            | Some t -> Error <| sprintf "Type mismatch. Expected %O, but got %O" typ t
            | None -> either {
                let! allProds =
                    Map.tryFind head allProductions
                    |> failIfNone (sprintf "No matching fusers for production %O" prod)
                let! firstNonRecursive =
                    allProds
                    |> Seq.tryPick (fun p -> if p.Production.Handle.Contains(head) |> not then Some Production else None)
                    |> failIfNone (sprintf "Production %O is always defined in terms of itself" prod)
                do! impl firstNonRecursive typ
                do productionToType.Add(prod, typ)
                return! Error ""
            }
        0

    let ofSeq2<'result> transformers fusers =
        0

    let typeCheck3<'result> transformers fusers =
        let productionsWithDifferentPossibleTypes =
            fusers
            |> Seq.groupBy (fun {Production = {Head = head}} -> head)
            |> Seq.filter (snd >> Seq.distinctBy (fun {OutputType = typ} -> typ) >> List.ofSeq >> (function | [x] -> Ok x | _ ->))
            |> Seq.map (fst >> sprintf "Nonterminal %O might get fused into objects of different types" >> Error)
            |> collect
            |> Result.map ignore
        0
