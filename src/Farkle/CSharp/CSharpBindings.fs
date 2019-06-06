// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.CSharp

open Farkle
open System
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Farkle.Collections

module RF = RuntimeFarkle

type PP<'a> = Farkle.PostProcessor.PostProcessor<'a>

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods to easily create and work
/// with <see cref="RuntimeFarkle{TResult}"/>.</summary>
type RuntimeFarkleExtensions =
    static member syntaxChecker = 
        {new PP<obj> with
            member __.Transform(_, _, _) = null
            member __.Fuse(_, _) = null}
    static member private defaultEncoding enc = if isNull enc then Encoding.UTF8 else enc

    [<Extension>]
    /// <summary>Parses and post-processes a string.</summary>
    static member Parse(rf, str) = RF.parseString rf ignore str

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="ReadOnlyMemory{Char}"/>.</summary>
    static member Parse(rf, mem) = RF.parseMemory rf ignore mem

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="System.IO.Stream"/>,
    /// encoded with the given <see cref="System.Text.Encoding"/>.</summary>
    /// <remarks>If not specified, the encoding defaults to UTF-8.</remarks>
    static member Parse(rf, stream, [<Optional; DefaultParameterValue(null: Encoding)>] encoding) =
        let encoding = RuntimeFarkleExtensions.defaultEncoding encoding
        RF.parseStream rf ignore true encoding stream

    [<Extension>]
    /// <summary>Parses and post-processes the file at the given path, encoded with the given <see cref="Encoding"/>.</summary>
    /// <remarks>If not specified, the encoding defaults to UTF-8.</remarks>
    static member ParseFile(rf, fileName, [<Optional; DefaultParameterValue(null: Encoding)>] encoding) =
        let encoding = RuntimeFarkleExtensions.defaultEncoding encoding
        RF.parseFile rf ignore encoding fileName

    [<Extension>]
    /// <summary>Returns a new <see cref="RuntimeFarkle{TResult}"/> with a changed <see cref="PostProcessor"/>.</summary>
    static member ChangePostProcessor(rf: RuntimeFarkle<'TResult>, pp: Farkle.PostProcessor.PostProcessor<'TNewResult>) =
        RF.changePostProcessor pp rf

    [<Extension>]
    /// <summary>Returns a <see cref="RuntimeFarkle{Object}" /> that just checks if the given string is valid.</summary>
    /// <remarks>If syntax-checking succeeds, the value of the result will be always <c>null</c></remarks>
    static member SyntaxCheck(rf) = RF.changePostProcessor RuntimeFarkleExtensions.syntaxChecker rf

[<Sealed>]
/// <summary>An object that contains a function to convert a <see cref="Production"/> to an arbitrary object.</summary>
type Fuser private(f: Func<obj[], obj>) =
    member internal __.Invoke(x) = f.Invoke(x)
    /// <summary>Creates a <see cref="Fuser"/> from a delegate that accepts an array of objects.</summary> 
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="f">The delegate that converts the production's children into the desired object.</param>
    static member CreateRaw(f) = Fuser(f)

    /// <summary>Creates a <see cref="Fuser"/> that always returns a constant value.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="x">The object this fuser is always going to return.</param>
    static member Constant<'T>(x: 'T) = Fuser(fun _ -> x |> box)

    /// <summary>Creates a <see cref="Fuser"/> that returns the first symbol of the production unmodified.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    static member First = Fuser(fun x -> x.[0])

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from one of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx">The zero-based index of the symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T,'TResult>(idx, f: Func<'T, 'TResult>) =
        Fuser(fun x -> f.Invoke(x.[idx] :?> _) |> box)

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from two of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'TResult>(idx1, idx2, f: Func<'T1,'T2,'TResult>) =
        Fuser(fun x -> f.Invoke(x.[idx1] :?> _, x.[idx2] :?> _) |> box)

    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from three of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="idx3">The zero-based index of the third symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'T3,'TOutput>(idx1, idx2, idx3, f: Func<'T1,'T2,'T3,'TOutput>) =
        Fuser(fun x -> f.Invoke(x.[idx1] :?> _,x.[idx2] :?> _,x.[idx3] :?> _) |> box)

[<AbstractClass; Sealed; Extension>]
/// <summary>A helper class with methods to create a <see cref="PostProcessor{T}"/>.</summary>
type PostProcessor =

    /// <summary>Creates a <see cref="PostProcessor{TResult}"/> from the given transformers and fusers.</summary>
    /// <typeparam name="TResult">The type of the final object this post-processor will return from a gramamr.</typeparam>
    /// <param name="fTransform">A delegate that accepts the table index of a terminal, its position, and its data,
    /// and returns an arbitrary object</param>
    /// <param name="fGetFuser">A delegate that accepts the table index of a <see cref="Farkle.Grammar.Production"/>
    /// and returns its appropriate <see cref="Fuser"/></param>
    static member Create<'TResult> (fTransform: CharStreamCallback<uint32>, fGetFuser: Func<uint32,Fuser>) =
        {new PP<'TResult> with
            member __.Transform(term, pos, data) = fTransform.Invoke(term.Index, pos, data)
            member __.Fuse(prod, arguments) = fGetFuser.Invoke(prod.Index).Invoke(arguments)}

    [<Extension>]
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from a
    /// <see cref="PostProcessor{TResult}"/> and a grammar file in Base-64 format</summary>
    static member CombineWithBase64Grammar<'TResult> (pp: PP<'TResult>, grammar) = RF.ofBase64String pp grammar
