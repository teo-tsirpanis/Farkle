// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.CSharp

open Farkle.IO
open Farkle.PostProcessor
open System
open System.Collections.Immutable

type PP<'a> = PostProcessor<'a>

[<Sealed>]
/// <summary>An object that contains a function to convert a <see cref="Production"/> to an arbitrary object.</summary>
type Fuser private(f: Func<obj[], obj>) =
    member internal __.Invoke(x) = f.Invoke(x)
    /// <summary>Creates a <see cref="Fuser"/> from a delegate that accepts an array of objects.</summary>
    /// <param name="f">The delegate that converts the production's children into the desired object.</param>
    static member CreateRaw(f) = Fuser(f)

    /// <summary>Creates a <see cref="Fuser"/> that always returns a constant value.</summary>
    /// <param name="x">The object this fuser is always going to return.</param>
    static member Constant<'T>(x: 'T) = Fuser(fun _ -> x |> box)

    /// <summary>Creates a <see cref="Fuser"/> that returns the first symbol of the production unmodified.</summary>
    static member First = Fuser(fun x -> x.[0])

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from one of its symbols.</summary>
    /// <param name="idx">The zero-based index of the symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T,'TResult>(idx, f: Func<'T, 'TResult>) =
        Fuser(fun x -> f.Invoke(x.[idx] :?> _) |> box)

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from two of its symbols.</summary>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'TResult>(idx1, idx2, f: Func<'T1,'T2,'TResult>) =
        Fuser(fun x -> f.Invoke(x.[idx1] :?> _, x.[idx2] :?> _) |> box)

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from three of its symbols.</summary>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="idx3">The zero-based index of the third symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'T3,'TOutput>(idx1, idx2, idx3, f: Func<'T1,'T2,'T3,'TOutput>) =
        Fuser(fun x -> f.Invoke(x.[idx1] :?> _,x.[idx2] :?> _,x.[idx3] :?> _) |> box)

[<AbstractClass; Sealed>]
/// <summary>A helper class with methods to create a <see cref="PostProcessor{T}"/>.</summary>
type PostProcessor =

    /// <summary>Creates a <see cref="PostProcessor{TResult}"/> from the given transformers and fusers.</summary>
    /// <typeparam name="TResult">The type of the final object this post-processor will return from a gramamr.</typeparam>
    /// <param name="fTransform">A delegate that accepts the table index of a terminal, its position, and its data,
    /// and returns an arbitrary object</param>
    /// <param name="fGetFuser">A delegate that accepts the table index of a <see cref="Farkle.Grammar.Production"/>
    /// and returns its appropriate <see cref="Fuser"/></param>
    static member Create<'TResult> (fTransform: CharStreamCallback<uint32>, fGetFuser: Func<uint32,Fuser>) =
        let fusers =
            let b = ImmutableArray.CreateBuilder()
            let mutable i = 0u
            let mutable fuser = fGetFuser.Invoke(i)
            while fuser <> Unchecked.defaultof<_> do
                b.Add fuser
                i <- i + 1u
                fuser <- fGetFuser.Invoke(i)
            b.ToImmutable()
        {new PP<'TResult> with
            member __.Transform(term, pos, data) = fTransform.Invoke(term.Index, pos, data)
            member __.Fuse(prod, arguments) =
                let idx = int prod.Index
                if idx < fusers.Length then
                    // There is no need to check for null.
                    // It is already taken care of it earlier.
                    fusers.[idx].Invoke(arguments)
                else
                    raise FuserNotFound}

    /// <summary>A <see cref="PostProcessor{Object}"/> that just checks if the given string is valid.</summary>
    /// <remarks>It always returns <c>null</c>.</remarks>
    static member SyntaxChecker =
        {new PP<obj> with
            member __.Transform(_, _, _) = null
            member __.Fuse(_, _) = null}
