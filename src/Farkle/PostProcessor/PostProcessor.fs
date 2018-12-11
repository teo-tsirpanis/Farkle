// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar2
open System

/// Post-processors convert strings of a grammar into more meaningful
/// types for the library that uses the parser.
/// The type in question is the argument of this post-processor type.
type PostProcessor<'a> =
    /// Converts a generic token into an arbitrary object.
    /// In case of an insignificant token, implementations can return a boxed `()`, or `null`.
    abstract Transform: Terminal -> Position -> ReadOnlySpan<char> -> obj
    /// Fuses the many members of a production into one object.
    /// Fusing production rules must always succeed. In very case of an error like
    /// an unrecognized production, the function must return `false`.
    abstract Fuse: Production -> obj[] -> outref<obj> -> bool

/// Functions to create `PostProcessor`s, as well as some ready to use.
module PostProcessor =

    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    let syntaxCheck =
        {new PostProcessor<unit> with
            member __.Transform _ _ _ = box ()
            member __.Fuse _ _ output =
                output <- ()
                true}

    /// This post-processor creates a domain-ignorant `AST` that contains the information
    let ast =
        {new PostProcessor<AST> with
            member __.Transform sym pos x = AST.Content {Symbol = sym; Position = pos; Data = x.ToString()} |> box
            member __.Fuse prod items output =
                output <- items |> Seq.cast |> List.ofSeq |> curry AST.Nonterminal prod
                true}

    /// Creates a `PostProcessor` from the given sequences of `Transformer`s, and `Fuser`s.
    // TODO: Add type checking.
    // This is an extremely dangerous API, where any notion of
    // static typing is swept under the mat of the `Object` type.
    // My proposal is to make this function fallible, by checking
    // whether types match each other before the post-processor gets created.
    let ofSeq<'result> transformers fusers =
        let transformers = transformers |> Seq.map (fun {SymbolIndex = sym; TheTransformer = f} -> sym, f) |> Map.ofSeq
        let fusers = fusers |> Seq.map (fun {ProductionIndex = prod; TheFuser = f} -> prod, f) |> Map.ofSeq
        {new PostProcessor<'result> with
            member __.Transform token =
                token.Symbol
                |> Symbol.tryGetTerminalIndex
                |> Option.bind transformers.TryFind
                |> Option.map ((|>) token.Data)
                |> Option.defaultValue null
            member __.Fuse(prod,arguments,output) =
                match fusers.TryFind prod.Index with
                | Some f ->
                    output <- f arguments
                    not <| isNull output
                | None -> false
        }
