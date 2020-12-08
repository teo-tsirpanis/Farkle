// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammar
open System
open System.Runtime.CompilerServices

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this post-processor
/// will return from a grammar.</typeparam>
type PostProcessor<[<CovariantOut>] 'T> =
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    abstract Fuse: Production * ReadOnlySpan<obj> -> [<Nullable(2uy)>] obj
    inherit ITransformer<Terminal>

/// Some reusable `PostProcessor`s.
module PostProcessors =

    [<CompiledName("SyntaxChecker"); Nullable(1uy, 2uy)>]
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
            member _.Transform (sym, context, data) =
                AST.Content(sym, context.StartPosition, data.ToString()) |> box
            member _.Fuse (prod, members) =
                let mutable xs = []
                for i = members.Length - 1 downto 0 do
                    xs <- members.[i] :?> AST :: xs
                AST.Nonterminal(prod, xs) |> box}
