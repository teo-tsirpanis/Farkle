// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammars
open System
open System.Runtime.CompilerServices

/// <summary>The base interface implemented by <see cref="T:Farkle.IPostProcessor`1"/>.
/// Custom post-processors should not directly implement this interface.</summary>
/// <seealso cref="T:Farkle.IPostProcessor`1"/>
type IPostProcessor =
    /// <summary>Converts a token into an object.</summary>
    /// <param name="symbol">An object identifying the kind of the token.</param>
    /// <param name="context">A <see cref="ITransformerContext"/> object
    /// that provides more information about the token.</param>
    /// <param name="data">A read-only span of the token's characters.</param>
    /// <returns>An object. It can be <see langword="null"/>.</returns>
    abstract Transform: symbol: Terminal * context: ITransformerContext * data: ReadOnlySpan<char>
        -> [<Nullable(2uy)>] obj
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    /// <param name="production">The production whose members will be fused.</param>
    /// <param name="members">A read-only span of the production's members</param>
    /// <returns>An object. It can be <see langword="null"/>.</returns>
    abstract Fuse: production: Production * members: ReadOnlySpan<obj> -> [<Nullable(2uy)>] obj

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <remarks>The type's actual implementation is in the base
/// <see cref="T:Farkle.IPostProcessor"/> interface.</remarks>
/// <typeparam name="T">The type of the final object this
/// post-processor will return from a grammar. This generic
/// parameter is covariant.</typeparam>
/// <seealso cref="T:Farkle.IPostProcessor"/>
type IPostProcessor<[<CovariantOut; Nullable(2uy)>] 'T> =
    inherit IPostProcessor

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
