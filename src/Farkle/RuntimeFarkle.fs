// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open System.Runtime.InteropServices
open Farkle.Builder
open Farkle.Grammar
open Farkle.IO
open Farkle.Parser
open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Text

/// A type signifying an error during the parsing process.
[<RequireQualifiedAccess>]
type FarkleError =
    /// There was a parsing error.
    | ParseError of ParserError
    /// There had been an error while building the grammar .
    | BuildError of BuildError
    override x.ToString() =
        match x with
        | ParseError x -> string x
        | BuildError x -> sprintf "Error while building the grammar: %O" x

/// <summary>A reusable parser and post-processor,
/// created for a specific grammar, and returning
/// a specific type of object that best describes
/// an expression of the language of this grammar.</summary>
/// <remarks><para>Its parsing methods return an F# result
/// type containing either the post-processed return
/// type, or a type describing what did wrong and where.</para>
/// <para>Exceptions during post-processing (apart from
/// <see cref="ParserApplicationException"/>) are thrown
/// after being wrapped in a <see cref="PostProcessorException"/>.</para></remarks>
[<NoComparison; ReferenceEquality>]
type RuntimeFarkle<'TResult> = internal {
    Grammar: Result<Grammar,BuildError>
    PostProcessor: PostProcessor<'TResult>
}
with
    static member internal CreateMaybe postProcessor grammarMaybe =
        {
            Grammar = grammarMaybe
            PostProcessor = postProcessor
        }
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(grammar, postProcessor) =
        grammar
        |> Ok
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(fileName, postProcessor) =
        fileName
        |> EGT.ofFile
        |> Ok
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// EGT file encoded in Base64 and <see cref="PostProcessor{TResult}"/>.</summary>
    static member CreateFromBase64String(str, postProcessor) =
        str
        |> EGT.ofBase64String
        |> Ok
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// Returns whether building was successful.
    /// If loaded from an EGT file, it will always return true.
    member this.IsBuildSuccessful =
        match this.Grammar with
        | Ok _ -> true
        | Error _ -> false

    /// Returns a domain-specific error object that
    /// describes what had gone wrong while building,
    /// or raises an exception if building had been successful.
    member this.GetBuildError() =
        match this.Grammar with
        | Ok _ -> invalidOp "Building the grammar did not fail."
        | Error msg -> msg

    /// Returns a user-friendly error message that
    /// describes what had gone wrong while building,
    /// or an empty string if building had been successful.
    member this.GetBuildErrorMessage() =
        match this.Grammar with
        | Ok _ -> String.Empty
        | Error msg -> string msg

    /// <summary>Gets the <see cref="Farkle.Grammar.Grammar"/>
    /// behind the <see cref="RuntimeFarkle{TResult}"/>.</summary>
    /// <exception cref="System.Exception">Building the grammar
    /// had failed. The exception's message contains further details.</exception>
    member this.GetGrammar() =
        match this.Grammar with
        | Ok grammar -> grammar
        | Error msg -> msg |> string |> failwith

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = {
        Grammar = rf.Grammar
        PostProcessor = pp
    }

    /// Changes the post-processor of a runtime Farkle to a
    /// dummy one suitable for syntax-checking, not parsing.
    let syntaxCheck rf = changePostProcessor PostProcessors.syntaxCheck rf

    /// Creates a `RuntimeFarkle` from the given grammar and post-processor.
    [<CompiledName("Create")>]
    let create postProcessor (grammar: Grammar) =
        RuntimeFarkle<_>.Create(grammar, postProcessor)

    /// Creates a `RuntimeFarkle` from the given
    /// post-processor, and the .egt file at the given path.
    /// In case the grammar file fails to be read,
    /// an exception will be raised.
    [<CompiledName("CreateFromFile")>]
    let ofEGTFile postProcessor (fileName: string) =
        RuntimeFarkle<_>.Create(fileName, postProcessor)

    /// Creates a `RuntimeFarkle` from the given post-processor,
    /// and the given Base64 representation of an .egt file.
    /// In case the grammar file fails to be read,
    /// an exception will be raised.
    [<CompiledName("CreateFromBase64String")>]
    let ofBase64String postProcessor x =
        RuntimeFarkle<_>.CreateFromBase64String(x, postProcessor)

    /// Creates a `RuntimeFarkle` from the given `DesigntimeFarkle&lt;'T&gt;`.
    /// In case there is a problem with the grammar,
    /// the `RuntimeFarkle` will fail every time it is used.
    /// If the designtime Farkle is marked for precompilation and a suitable
    /// precompiled grammar is found, building it again will be avoided.
    [<CompiledName("Build")>]
    let build df =
        let theFabledGrammar = PrecompilerInterface.getGrammarOrBuild df
        let theTriumphantPostProcessor = DesigntimeFarkleBuild.buildPostProcessorOnly df
        RuntimeFarkle<_>.CreateMaybe theTriumphantPostProcessor theFabledGrammar

    /// Creates a syntax-checking `RuntimeFarkle`
    /// from an untyped `DesigntimeFarkle`.
    [<CompiledName("BuildUntyped")>]
    let buildUntyped df =
        df
        |> PrecompilerInterface.getGrammarOrBuild
        |> RuntimeFarkle<_>.CreateMaybe PostProcessors.syntaxCheck

    [<CompiledName("MarkForPrecompile"); MethodImpl(MethodImplOptions.NoInlining)>]
    /// Marks the given designtime as available to have its grammar
    /// precompiled ahead of time. See more, including usage restrictions
    /// on https://teo-tsirpanis.github.io/Farkle/the-precompiler.html
    let markForPrecompile df =
        let asm = Assembly.GetCallingAssembly()
        PrecompilerInterface.prepare df asm

    [<CompiledName("MarkForPrecompileU"); MethodImpl(MethodImplOptions.NoInlining)>]
    /// The untyped edition of `markForPrecompile`.
    let markForPrecompileU df =
        let asm = Assembly.GetCallingAssembly()
        PrecompilerInterface.prepareU df asm

    /// This designtime Farkle is used in the tests to test
    /// whether an object from a different assembly is eligible
    /// for precompilation (it isn't, unless it is marked again).
    let internal dummyPrecompilable =
        "Dummy" ||= [empty =% 521]
        |> markForPrecompile

    /// Parses and post-processes a `CharStream`.
    [<CompiledName("ParseChars")>]
    let parseChars (rf: RuntimeFarkle<'TResult>) input =
        let mkError msg = msg |> FarkleError.ParseError |> Error
        match rf.Grammar with
        | Ok grammar ->
            let tokenizer = DefaultTokenizer grammar
            try
                LALRParser.parse grammar rf.PostProcessor tokenizer input |> Ok
            with
            | :? ParserException as e -> mkError e.Error
            | :? ParserApplicationException as e ->
                ParserError(input.CurrentPosition, ParseErrorType.UserError e.Message)
                |> mkError
        | Error x -> Error <| FarkleError.BuildError x

    [<CompiledName("ParseMemory")>]
    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    let parseMemory rf (input: ReadOnlyMemory<_>) =
        use cs = new CharStream(input)
        parseChars rf cs

    /// Parses and post-processes a string.
    [<CompiledName("ParseString")>]
    let parseString rf (inputString: string) =
        use cs = new CharStream(inputString)
        parseChars rf cs

    /// Parses and post-processes a .NET `Stream` with the
    /// given character encoding, which may be lazily read.
    /// Better use `parseTextReader` instead.
    [<CompiledName("ParseStream"); Obsolete("Streams are supposed to \
contain binary data; not text. Use parseTextReader instead.")>]
    let parseStream rf doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        let encoding = if isNull encoding then Encoding.UTF8 else encoding
        use sr = new StreamReader(inputStream, encoding, true, 4096, true)
        use cs =
            match doLazyLoad with
            | true -> new CharStream(sr)
            | false -> new CharStream(sr.ReadToEnd())
        parseChars rf cs

    /// Parses and post-processes a .NET `TextReader`. Its content is lazily read.
    [<CompiledName("ParseTextReader")>]
    let parseTextReader rf (textReader: TextReader) =
        let cs = new CharStream(textReader)
        parseChars rf cs

    /// Parses and post-processes a file at the given path.
    [<CompiledName("ParseFile")>]
    let parseFile rf inputFile =
        use s = File.OpenText(inputFile)
        parseTextReader rf s

    /// Parses and post-processes a string.
    [<CompiledName("Parse"); Obsolete("Use parseString.")>]
    let parse rf x = parseString rf x

    let internal syntaxCheckerObj =
        unbox<PostProcessor<obj>> PostProcessors.syntaxCheck

open RuntimeFarkle

#nowarn "44"

type RuntimeFarkle<'TResult> with
    /// <summary>Parses and post-processes a <see cref="Farkle.IO.CharStream"/>.</summary>
    /// <param name="charStream">The character stream to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse charStream = parseChars this charStream
    /// <summary>Parses and post-processes a string.</summary>
    /// <param name="charStream">The string to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse mem = parseMemory this mem
    /// <summary>Parses and post-processes a <see cref="ReadOnlyMemory{Char}"/>.</summary>
    /// <param name="charStream">The read-only memory to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse str = parseString this str
    /// <summary>Parses and post-processes a
    /// <see cref="System.IO.Stream"/>.</summary>
    /// <param name="stream">The stream to parse.</param>
    /// <param name="encoding">The character encoding of
    /// the stream's data. Defaults to UTF-8.</param>
    /// <param name="doLazyLoad">Whether to gradually read the
    /// input instead of reading its entirety in memory.
    /// Defaults to <see langword="true"/>.</param>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    [<Obsolete("Streams are supposed to contain binary \
data; not text. Parse a TextReader instead.")>]
    member this.Parse(stream, [<Optional>] encoding, [<Optional>] doLazyLoad) =
        parseStream this stream encoding doLazyLoad
    /// <summary>Parses and post-processes a <see cref="System.IO.TextReader"/>.</summary>
    /// <param name="charStream">The string to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    /// <remarks>The text reader's content will be lazily read</remarks>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse textReader = parseTextReader this textReader
    /// <summary>Changes the <see cref="PostProcessor"/> of this runtime Farkle.</summary>
    /// <param name="pp">The new post-processor.</param>
    /// <returns>A new runtime Farkle with ite post-
    /// processor changed to <paramref name="pp"/>.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.ChangePostProcessor pp = changePostProcessor pp this
    /// <summary>Changes the <see cref="PostProcessor"/> of this runtime Farkle to a dummy
    /// one that is useful for syntax-checking, not parsing.</summary>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.SyntaxCheck() =
        changePostProcessor syntaxCheckerObj this
