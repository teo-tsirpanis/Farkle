// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar
open System
open System.Collections.Generic

/// Post-processors convert strings of a grammar into more meaningful
/// types for the library that uses the parser.
/// The type in question is the argument of this post-processor type.
type PostProcessor<'a> =
    /// Converts a generic token into an arbitrary object.
    /// In case of an insignificant token, implementations can return a boxed `()`, or `null`.
    abstract Transform: Terminal * Position * ReadOnlySpan<char> -> obj
    /// Fuses the many members of a production into one object.
    /// Fusing production rules must always succeed. In very case of an error like
    /// an unrecognized production, the function must return `false`.
    abstract Fuse: Production * obj[] * outref<obj> -> bool

/// Functions to create `PostProcessor`s, as well as some ready to use.
module PostProcessor =

    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    let syntaxCheck =
        {new PostProcessor<unit> with
            member __.Transform (_, _, _) = box ()
            member __.Fuse (_, _, output) =
                output <- ()
                true}

    /// This post-processor creates a domain-ignorant `AST` that contains the information
    let ast =
        {new PostProcessor<AST> with
            member __.Transform (sym, pos, x) = AST.Content {Symbol = sym; Position = pos; Data = x.ToString()} |> box
            member __.Fuse (prod, items, output) =
                output <- items |> Seq.cast |> List.ofSeq |> curry AST.Nonterminal prod
                true}

    let private typeCheck<'result> (grammar: Grammar) transformers fusers =
        let checkTerminal =
            let fCheck =
                let map =
                    transformers
                    |> Seq.map (fun {SymbolIndex = sym; OutputType = typ} -> sym, typ)
                    |> Map.ofSeq
                map.TryFind >> Option.defaultValue typeof<obj>
            fun (Terminal (idx, _)) (typ: Type) -> typ.IsAssignableFrom(fCheck idx)
        let ntDict = Dictionary()
        let rec checkNonterminal (Nonterminal (idx, _) as nont) (typ: Type) =
            match ntDict.ContainsKey idx with
            | true -> typ.IsAssignableFrom(ntDict.[idx])
            | false ->
                let productions =
                    grammar.GetNonterminalInfo nont
                    |> Seq.sortBy (fun x -> x.Index)
                    // |> Seq.cache
                let fusers =
                    fusers
                    |> Seq.filter (fun {ProductionIndex = idx} -> productions |> Seq.exists (fun x -> x.Index = idx))
                    |> Seq.sortBy (fun x -> x.ProductionIndex)
                    // |> Seq.cache
                let indicesMatch =
                    Seq.length productions = Seq.length fusers
                    // && (productions, fusers) ||> Seq.forall2 (fun prod f -> prod.Index = f.ProductionIndex)
                let outputType =
                    fusers
                    |> Seq.map (fun {OutputType = x} -> x)
                    |> Seq.distinct
                    |> Array.ofSeq
                match outputType with
                | [|outputType|] when indicesMatch ->
                    ntDict.Add(idx, outputType)
                    let fCheckPair {Handle = prod} {InputTypes = types} =
                        let fCheckSymbol sym typ =
                            match sym, typ with
                            | Choice1Of2 term, Some typ -> checkTerminal term typ
                            | Choice2Of2 nont, Some typ -> checkNonterminal nont typ
                            | _, None -> true
                        Seq.forall2 fCheckSymbol prod types
                    Seq.forall2 fCheckPair productions fusers
                    && checkNonterminal nont typ // A second call will check the output type.
                | _ -> false
        checkNonterminal grammar.StartSymbol typeof<'result>

    /// Creates a `PostProcessor` from the given sequences of `Transformer`s, and `Fuser`s.
    /// The types of their functions are checked for correctness, in case of a failure, `None` will be returned.
    let ofSeq<'result> transformers fusers grammar =
        if typeCheck<'result> grammar transformers fusers then
            let transformers = transformers |> Seq.map (fun {SymbolIndex = sym; TheTransformer = f} -> sym, f) |> Map.ofSeq
            let fusers = fusers |> Seq.map (fun {ProductionIndex = prod; TheFuser = f} -> prod, f) |> Map.ofSeq
            Some {
                new PostProcessor<'result> with
                member __.Transform (sym, pos, data) =
                    match transformers.TryFind(sym.Index) with
                    | Some f -> f.Invoke(pos, data)
                    | None -> null
                member __.Fuse(prod,arguments,output) =
                    match fusers.TryFind prod.Index with
                    | Some f ->
                        output <- f arguments
                        not <| isNull output
                    | None -> false
            }
        else
            None
