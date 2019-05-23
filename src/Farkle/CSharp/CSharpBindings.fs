// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.CSharp

open Farkle
open Farkle.PostProcessor
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

module RF = RuntimeFarkle

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods to easilycreate and work
/// with <see cref="RuntimeFarkle{TResult}"/>.</summary>
type RuntimeFarkleExtensions =
    static member private defaultEncoding enc = if isNull enc then Encoding.UTF8 else enc
    [<Extension>]
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor"/>.</summary>
    static member Create(grammar, postProcessor) = RF.create postProcessor grammar
    [<Extension>]
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file and <see cref="PostProcessor"/>.</summary>
    static member Create(fileName, postProcessor) = RF.ofEGTFile postProcessor fileName
    [<Extension>]
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file encoded in Base-64 and <see cref="PostProcessor"/>.</summary>
    static member CreateFromBase64String(str, postProcessor) = RF.ofBase64String postProcessor str
    [<Extension>]
    /// <summary>Parses and post-processes a string.</summary>
    static member Parse(rf, str) = RF.parseString rf ignore str
    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="ReadOnlyMemory{Char}"/>.</summary>
    static member Parse(rf, mem) = RF.parseMemory rf ignore mem
    [<Extension>]
    /// <summary>Parses and post-processes a <see cref="Stream"/>,
    /// encoded with the given <see cref="Encoding"/>.</summary>
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

[<AbstractClass; Sealed>]
/// <summary>A helper class with methods to create a <see cref="PostProcessor{T}"/>.</summary>
type PostProcessor =
    /// <summary>Creates a <see cref="PostProcessor{T}"/> from the given transformers and fusers.</summary>
    /// <typeparam name="T">The type of the final object this post-processor will return from a gramamr.</typeparam>
    static member Create<'T>(transformers, fusers) = PostProcessor.ofSeq<'T> transformers fusers
    /// Returns a post-processor that just checks if the given string is valid.
    static member SyntaxCheck = PostProcessor.syntaxCheck

[<AbstractClass; Sealed>]
/// <summary>A helper class with methods to create a <see cref="Transformer"/>.</summary>
type Transformer =
    /// <summary>Creates a <see cref="Transformer"/> from a <see cref="TransformerCallback{T}"/>.</summary>
    /// <param name="sym">The index of the terminal to transform.</param>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="f">The delegate that converts the terminal's data into the desired object.</param>
    static member Create(sym: uint32, f: C<'T>) = Transformer.create sym f
    /// <summary>Creates a <see cref="Transformer"/> from a <see cref="PositionedTransformerCallback{T}"/>.</summary>
    /// <param name="sym">The index of the terminal to transform.</param>
    /// <param name="f">The delegate that converts the terminal's position and data into the desired object.</param>
    static member Create(sym: uint32, f: C2<'T>) = Transformer.createP sym f

[<AbstractClass; Sealed>]
/// <summary>A helper class with methods to create a <see cref="Fuser"/>.</summary>
type Fuser =
    /// <summary>Creates a <see cref="Fuser"/> from a delegate that accepts an array of objects.</summary> 
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="f">The delegate that converts the production's children into the desired object.</param>
    static member CreateRaw(prod: uint32, f) = Fuser.create prod <| FuncConvert.FromFunc<_,_> f
    /// <summary>Creates a <see cref="Fuser"/> that always returns a constant value.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="x">The object this fuser is always going to return.</param>
    static member Constant<'T>(prod: uint32, x: 'T) = Fuser.constant prod x
    /// <summary>Creates a <see cref="Fuser"/> that returns the first symbol of the production unmodified.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    static member Identity(prod: uint32) = Fuser.identity prod
    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from one of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx">The zero-based index of the symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T,'TResult>(prod: uint32, idx, f) =
        Fuser.take1Of prod idx <| FuncConvert.FromFunc<'T,'TResult> f
    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from two of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'TOutput>(prod: uint32, idx1, idx2, f) =
        Fuser.take2Of prod (idx1, idx2) <| FuncConvert.FromFunc<'T1,'T2,'TOutput> f
    /// <summary>Creates a <see cref="Fuser"/> that fuses a <see cref="Production"/> from three of its symbols.</summary>
    /// <param name="prod">The index of the <see cref="Production"/> to transform.</param>
    /// <param name="idx1">The zero-based index of the first symbol of interest.</param>
    /// <param name="idx2">The zero-based index of the second symbol of interest.</param>
    /// <param name="idx3">The zero-based index of the third symbol of interest.</param>
    /// <param name="f">The delegate that converts the symbols into the desired object.</param>
    static member Create<'T1,'T2,'T3,'TOutput>(prod: uint32, idx1, idx2, idx3, f) =
        Fuser.take3Of prod (idx1, idx2, idx3) <| FuncConvert.FromFunc<'T1,'T2,'T3,'TOutput> f
