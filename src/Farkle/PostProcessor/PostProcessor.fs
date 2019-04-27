// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar
open System
open System.Collections.Generic

/// An exception that gets thrown when a post-processor does not find the appropriate `Fuser` for a production.
/// This means that the post-processor is not properly configured.
exception FuserNotFound of Production

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
    abstract Fuse: Production * obj[] -> obj

/// Functions to create `PostProcessor`s, as well as some ready to use.
module PostProcessor =

    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    let syntaxCheck =
        {new PostProcessor<unit> with
            member __.Transform (_, _, _) = null
            member __.Fuse (_, _) = null}

    /// This post-processor creates a domain-ignorant `AST`.
    let ast =
        {new PostProcessor<AST> with
            member __.Transform (sym, pos, x) = AST.Content {Symbol = sym; Position = pos; Data = x.ToString()} |> box
            member __.Fuse (prod, items) = items |> Seq.cast |> List.ofSeq |> curry AST.Nonterminal prod |> box}

    /// Creates a `PostProcessor` from the given sequences of `Transformer`s, and `Fuser`s.
    let ofSeq<'result> transformers fusers =
        let transformers = transformers |> Seq.map (fun {SymbolIndex = sym; TheTransformer = f} -> sym, f) |> Map.ofSeq
        let fusers = fusers |> Seq.map (fun {ProductionIndex = prod; TheFuser = f} -> prod, f) |> Map.ofSeq
        {new PostProcessor<'result> with
            member __.Transform (sym, pos, data) =
                /// It is very likely that a transformer will not be found.
                /// Throwing an exception would destroy performance, but it will
                /// be caught nevertheless, in case of an error inside the transformer.
                match transformers.TryGetValue(sym.Index) with
                | true, f -> f.Invoke(pos, data)
                | false, _ -> null
            member __.Fuse(prod, arguments) =
                /// But if a fuser is not found, it is always an error stemming from incorrect configuration; not input,
                /// so an exception will not hurt, and will be caught by the LALR parser.
                match fusers.TryGetValue(prod.Index) with
                | true, f -> f arguments
                | false, _ -> raise <| FuserNotFound prod}
