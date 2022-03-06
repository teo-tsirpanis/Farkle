// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammars
open System
open System.Runtime.CompilerServices

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this
/// post-processor will return from a grammar. This generic
/// parameter is covariant.</typeparam>
type IPostProcessor<[<CovariantOut; Nullable(2uy)>] 'T> =
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    /// <param name="production">The production whose members will be fused.</param>
    /// <param name="members">A read-only span of the production's members</param>
    /// <returns>An object. It can be <see langword="null"/>.</returns>
    abstract Fuse: production: Production * members: ReadOnlySpan<obj> -> [<Nullable(2uy)>] obj
    inherit ITransformer<Terminal>

/// To be implemented by Farkle's post-processors that are
/// interested in more detailed events about their use.
type internal PostProcessorEventListener =
    /// Notifies the post-processor that a parsing operation has started.
    abstract ParsingStarted: unit -> unit

/// Some reusable `PostProcessor`s.
module PostProcessors =

    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    [<CompiledName("SyntaxChecker"); Nullable(1uy, 2uy)>]
    let syntaxCheck =
        {new IPostProcessor<unit> with
            member _.Transform (_, _, _) = null
            member _.Fuse (_, _) = null}

    /// This post-processor creates a domain-ignorant `AST`.
    [<CompiledName("AST")>]
    let ast =
        {new IPostProcessor<AST> with
            member _.Transform (sym, context, data) =
                AST.Content(sym, context.StartPosition, data.ToString()) |> box
            member _.Fuse (prod, members) =
                let mutable xs = []
                for i = members.Length - 1 downto 0 do
                    xs <- members.[i] :?> AST :: xs
                AST.Nonterminal(prod, xs) |> box}
