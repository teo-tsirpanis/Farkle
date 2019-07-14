// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.CSharp

open Farkle
open Farkle.Collections
open Farkle.PostProcessor
open System
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

module RF = RuntimeFarkle

type PP<'a> = PostProcessor<'a>

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods to easily create and work
/// with <see cref="RuntimeFarkle{TResult}"/>.</summary>
type RuntimeFarkleExtensions =

    static member private fLogIgnore = fun (_: Parser.ParseMessage) -> ()

    static member private defaultLogFunc f = if isNull f then RuntimeFarkleExtensions.fLogIgnore else FuncConvert.FromAction<_> f

    static member private defaultEncoding enc = if isNull enc then Encoding.UTF8 else enc

    /// <summary>A <see cref="PostProcessor{Object}"/> that just checks if the given string is valid.</summary>
    /// <remarks>It always returns <c>null</c>.</remarks>
    static member SyntaxChecker = 
        {new PP<obj> with
            member __.Transform(_, _, _) = null
            member __.Fuse(_, _) = null}

    [<Extension>]
    /// <summary>Parses and post-processes a string.</summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for ever parsing event that happens.</param>
    static member Parse(rf, str, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseString rf fLog str

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="ReadOnlyMemory{Char}"/>.</summary>
    /// <param name="mem">The memory object to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for ever parsing event that happens.</param>s
    static member Parse(rf, mem, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseMemory rf fLog mem

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="System.IO.Stream"/>.</summary>
    /// <param name="stream">The stream to parse.</param>
    /// <param name="encoding">The character encoding of the stream's data. Defaults to UTF-8.</param>
    /// <param name="doLazyLoad">Whether to gradually read the input instead of reading its entirety in memory.
    /// Defaults to <c>true</c>.</param>
    /// <param name="fLog">An optional delegate that gets called for ever parsing event that happens.</param>
    static member Parse(rf, stream, [<Optional>] encoding, [<Optional; DefaultParameterValue(true)>] doLazyLoad, [<Optional>] fLog) =
        let encoding = RuntimeFarkleExtensions.defaultEncoding encoding
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseStream rf fLog doLazyLoad encoding stream

    [<Extension>]
    /// <summary>Parses and post-processes the file at the given path.</summary>
    /// <param name="fileName">The path of the file to parse.</param>
    /// <param name="encoding">The character encoding of the file's data. Defaults to UTF-8.</param>
    /// <param name="fLog">An optional delegate that gets called for ever parsing event that happens.</param>
    static member ParseFile(rf, fileName, [<Optional>] encoding, [<Optional>] fLog) =
        let encoding = RuntimeFarkleExtensions.defaultEncoding encoding
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseFile rf fLog encoding fileName

    [<Extension>]
    /// <summary>Returns a new <see cref="RuntimeFarkle{TResult}"/> with a changed <see cref="PostProcessor"/>.</summary>
    static member ChangePostProcessor(rf: RuntimeFarkle<'TResult>, pp: Farkle.PostProcessor.PostProcessor<'TNewResult>) =
        RF.changePostProcessor pp rf

    [<Extension>]
    /// <summary>Returns a <see cref="RuntimeFarkle{Object}"/> that just checks if its given input is valid.</summary>
    /// <remarks>If syntax-checking succeeds, the value of the result will be always <c>null</c></remarks>
    static member SyntaxCheck(rf) = RF.changePostProcessor RuntimeFarkleExtensions.SyntaxChecker rf

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
        {new PP<'TResult> with
            member __.Transform(term, pos, data) = fTransform.Invoke(term.Index, pos, data)
            member __.Fuse(prod, arguments) = 
                let theFuser = fGetFuser.Invoke(prod.Index)
                if theFuser = Unchecked.defaultof<_> then
                    raise FuserNotFound
                else
                    theFuser.Invoke(arguments)}
