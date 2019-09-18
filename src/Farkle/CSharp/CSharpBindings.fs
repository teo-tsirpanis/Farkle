// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

#nowarn "44" // This is for the RuntimeFarkle.ParseStream.

// I am pretty sure that Scott Wlaschin hates me right now...
// https://fsharpforfunandprofit.com/posts/cyclic-dependencies/
namespace rec Farkle.CSharp

open Farkle
open Farkle.Builder
open Farkle.IO
open Farkle.PostProcessor
open System
open System.Collections.Immutable
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

    /// <summary>Parses and post-processes a <see cref="Farkle.IO.CharStream"/>.</summary>
    /// <param name="cs">The <see cref="Farkle.IO.CharStream"/> to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member Parse(rf, cs, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseChars rf fLog cs

    [<Extension>]
    /// <summary>Parses and post-processes a string.</summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member Parse(rf, str, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseString rf fLog str

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="System.ReadOnlyMemory{Char}"/>.</summary>
    /// <param name="mem">The memory object to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member Parse(rf, mem, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseMemory rf fLog mem

    [<Extension; Obsolete("Streams are supposed to contain binary data; not text. Parse a TextReader instead.")>]
    /// <summary>Parses and post-processes a <see cref="System.IO.Stream"/>.</summary>
    /// <param name="stream">The stream to parse.</param>
    /// <param name="encoding">The character encoding of the stream's data. Defaults to UTF-8.</param>
    /// <param name="doLazyLoad">Whether to gradually read the input instead of reading its entirety in memory.
    /// Defaults to <c>true</c>.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member Parse(rf, stream, [<Optional>] encoding, [<Optional; DefaultParameterValue(true)>] doLazyLoad, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        let encoding = if isNull encoding then Encoding.UTF8 else encoding
        RF.parseStream rf fLog doLazyLoad encoding stream

    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="System.IO.TextReader"/>.</summary>
    /// <param name="textReader">The <see cref="System.IO.TextReader"/> to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member Parse(rf, textReader, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseTextReader rf fLog textReader

    [<Extension>]
    /// <summary>Parses and post-processes the file at the given path.</summary>
    /// <param name="fileName">The path of the file to parse.</param>
    /// <param name="fLog">An optional delegate that gets called for every parsing event that happens.</param>
    static member ParseFile(rf, fileName, [<Optional>] fLog) =
        let fLog = RuntimeFarkleExtensions.defaultLogFunc fLog
        RF.parseFile rf fLog fileName

    [<Extension>]
    /// <summary>Returns a new <see cref="RuntimeFarkle{TResult}"/> with a changed <see cref="PostProcessor"/>.</summary>
    static member ChangePostProcessor(rf: RuntimeFarkle<'TResult>, pp: PP<'TNewResult>) =
        RF.changePostProcessor pp rf

    [<Extension>]
    /// <summary>Returns a <see cref="RuntimeFarkle{Object}"/> that just checks if its given input is valid.</summary>
    /// <remarks>If syntax-checking succeeds, the value of the result will be always <c>null</c></remarks>
    static member SyntaxCheck(rf) = RF.changePostProcessor PostProcessor.SyntaxChecker rf

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods for the <see cref="DesigntimeFarkle{TResult}"/> type.</summary>
type DesigntimeFarkleExtensions =
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle{TResult}"/> into a <see cref="RuntimeFarkle{TResult}"/>.</summary>
    static member Build (df: DesigntimeFarkle<'TResult>) = RuntimeFarkle.build df
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle{TResult}"/>
    /// is case sensitive.</summary>
    static member CaseSensitive (df: DesigntimeFarkle<'TResult>, [<Optional; DefaultParameterValue(true)>] x) =
        DesigntimeFarkle.caseSensitive x df
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle{TResult}"/>
    /// would automatically ignore whitespace in input text.</summary>
    static member AutoWhitespace (df: DesigntimeFarkle<'TResult>, x) =
        DesigntimeFarkle.autoWhitespace x df
    [<Extension>]
    /// <summary>Adds a symbol specified by the given <see cref="Regex"/>
    /// that will be ignored in input text.</summary>
    static member AddNoiseSymbol (df: DesigntimeFarkle<'TResult>, name, regex) =
        DesigntimeFarkle.addNoiseSymbol name regex df
    [<Extension>]
    /// <summary>Adds a new line comment in the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    static member AddLineComment (df: DesigntimeFarkle<'TResult>, commentStart) =
        DesigntimeFarkle.addLineComment commentStart df
    [<Extension>]
    /// <summary>Adds a new block comment in the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    static member AddBlockComment (df: DesigntimeFarkle<'TResult>, commentStart, commentEnd) =
        DesigntimeFarkle.addBlockComment commentStart commentEnd df

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
