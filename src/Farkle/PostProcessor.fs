// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammar

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this post-processor
/// will return from a grammar.</typeparam>
type PostProcessor<[<CovariantOut>] 'T> =
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    abstract Fuse: Production * obj[] -> obj
    inherit ITransformer<Terminal>

/// Some reusable `PostProcessor`s.
module PostProcessors =

    [<CompiledName("SyntaxChecker")>]
    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    let syntaxCheck =
        {new PostProcessor<unit> with
            member _.Transform (_, _, _) = null
            member _.Fuse (_, _) = null}

    [<CompiledName("AST")>]
    /// This post-processor creates a domain-ignorant `AST`.
    let ast =
        {new PostProcessor<AST> with
            member _.Transform (sym, context, x) =
                AST.Content(sym, context.StartPosition, x.ToString()) |> box
            member _.Fuse (prod, items) =
                AST.Nonterminal(prod, items |> Seq.take prod.Handle.Length |> Seq.cast |> List.ofSeq) |> box}
