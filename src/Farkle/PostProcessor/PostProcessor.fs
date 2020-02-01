// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar
open System
open System.Collections.Immutable

/// An exception that gets thrown when a post-processor does not find the appropriate `Fuser` for a production.
/// This means that the post-processor is not properly configured.
exception internal FuserNotFound

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this post-processor will return from a gramamr.</typeparam>
type PostProcessor<[<CovariantOut>] 'T> =
    /// <summary>Converts a <see cref="Terminal"/> into an arbitrary object.</summary>
    /// <remarks>In case of an insignificant token, implementations can return <c>null</c></remarks>.
    abstract Transform: Terminal * Position * ReadOnlySpan<char> -> obj
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    /// <remarks>Fusing must always succeed. In very case of an error like
    /// an unrecognized production, the function has to throw an exception.</remarks>
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
            member __.Transform (sym, pos, x) = AST.Content(sym, pos, x.ToString()) |> box
            member __.Fuse (prod, items) = AST.Nonterminal(prod, items |> Seq.take prod.Handle.Length |> Seq.cast |> List.ofSeq) |> box}

    [<RequiresExplicitTypeArguments>]
    /// Creates a `PostProcessor` from the given sequences of `Transformer`s, and `Fuser`s.
    let ofSeq<'result> transformers fusers =
        let transformers =
            let maxElement = transformers |> Seq.map (fun (Transformer(term, _)) -> term) |> Seq.max |> int
            let b = ImmutableArray.CreateBuilder(maxElement + 1)
            for __ = 0 to maxElement do
                b.Add(null)
            transformers |> Seq.iter (fun (Transformer(prod, f)) -> b.[int prod] <- f)
            b.MoveToImmutable()
        let fusers =
            let maxElement = fusers |> Seq.map (fun (Fuser(prod, _)) -> prod) |> Seq.max |> int
            let b = ImmutableArray.CreateBuilder(maxElement + 1)
            for __ = 0 to maxElement do
                b.Add(null)
            fusers |> Seq.iter (fun (Fuser(prod, f)) -> b.[int prod] <- Func<_,_> f)
            b.MoveToImmutable()
        {new PostProcessor<'result> with
            member __.Transform (term, pos, data) =
                /// It is very likely that a transformer will not be found.
                /// Throwing an exception would destroy performance, but it will
                /// be caught nevertheless, in case of an error inside the transformer.
                match term.Index with
                | idx when idx < uint32 transformers.Length && not <| isNull transformers.[int idx] ->
                    transformers.[int idx].Invoke(pos, data)
                | _ -> null
            member __.Fuse(prod, arguments) =
                /// But if a fuser is not found, it is always an error stemming from incorrect configuration; not input,
                /// so an exception will not hurt, and will be caught by the LALR parser.
                match prod.Index with
                | idx when idx < uint32 fusers.Length && not <| isNull fusers.[int idx] ->
                    fusers.[int idx].Invoke(arguments)
                | _ -> raise FuserNotFound}
