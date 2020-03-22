// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

#nowarn "44" // This is for the RuntimeFarkle.ParseStream.

namespace Farkle

open Farkle
open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Text

module RF = RuntimeFarkle

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
    static member ChangePostProcessor(rf: RuntimeFarkle<'TResult>, pp: PostProcessor<'TNewResult>) =
        RF.changePostProcessor pp rf

    [<Extension>]
    /// <summary>Returns a <see cref="RuntimeFarkle{Object}"/> that just checks if its given input is valid.</summary>
    /// <remarks>If syntax-checking succeeds, the value of the result will be always <c>null</c></remarks>
    static member SyntaxCheck(rf) = RF.changePostProcessor (unbox<PostProcessor<obj>> PostProcessors.syntaxCheck) rf
