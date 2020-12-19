// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Builder
open Farkle.Grammar
open Farkle.IO
open Farkle.Parser
open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text

/// A type signifying an error during the parsing process.
[<RequireQualifiedAccess>]
type FarkleError =
    /// There was a parsing error.
    | ParseError of Error: ParserError
    /// There had been errors while building the grammar.
    | BuildError of Errors: BuildError list
    override x.ToString() =
        match x with
        | ParseError x -> x.ToString()
        | BuildError xs ->
            let sb = StringBuilder("Building the grammar failed:")
            match xs with
            | [x] -> sb.Append(' ').Append(x) |> ignore
            | xs ->
                for x in xs do
                    sb.AppendLine().Append(x) |> ignore
            sb.ToString()

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
type RuntimeFarkle<[<Nullable(2uy)>] 'TResult> = internal {
    Grammar: Result<Grammar,BuildError list>
    PostProcessor: PostProcessor<'TResult>
    TokenizerFactory: TokenizerFactory
}
with
    static member internal CreateMaybe postProcessor grammarMaybe =
        {
            Grammar = grammarMaybe
            PostProcessor = postProcessor
            TokenizerFactory = TokenizerFactory.Default
        }
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(grammar, postProcessor) =
        grammar
        |> Ok
        |> RuntimeFarkle<_>.CreateMaybe postProcessor

    /// Returns whether building was successful.
    /// If loaded from an EGT file, it will always return true.
    member this.IsBuildSuccessful =
        match this.Grammar with
        | Ok _ -> true
        | Error _ -> false

    /// Returns a list of `BuildError` objects that
    /// describe what had gone wrong while building, or returns
    /// an empty list if building had been successful.
    member this.GetBuildErrors() =
        match this.Grammar with
        | Ok _ -> []
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
    /// <exception cref="InvalidOperationException">Building the grammar
    /// had failed. The exception's message contains further details.</exception>
    member this.GetGrammar() =
        match this.Grammar with
        | Ok grammar -> grammar
        | Error msg -> msg |> string |> invalidOp
    /// <summary>Parses and post-processes a <see cref="Farkle.IO.CharStream"/>.</summary>
    /// <param name="input">The character stream to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    member this.Parse(input: CharStream) =
        let mkError msg = msg |> FarkleError.ParseError |> Error
        match this.Grammar with
        | Ok grammar ->
            let tokenizer = this.TokenizerFactory.CreateTokenizer grammar
            try
                LALRParser.parse grammar this.PostProcessor tokenizer input |> Ok
            with
            | :? ParserException as e -> mkError e.Error
            | :? ParserApplicationException as e ->
                ParserError(input.CurrentPosition, ParseErrorType.UserError e.Message)
                |> mkError
        | Error x -> Error <| FarkleError.BuildError x
    /// <summary>Changes the <see cref="TokenizerFactory"/> of this runtime Farkle.</summary>
    /// <param name="tokenizerFactory">The new tokenizer factory</param>
    /// <returns>A new runtime Farkle that will parse text with the tokenizer
    /// <paramref name="tokenizerFactory"/> will create.</returns>
    /// <remarks>A new tokenizer will be created with each parse operation.</remarks>
    member this.ChangeTokenizer tokenizerFactory =
        {this with TokenizerFactory = tokenizerFactory}
    /// <summary>Changes the <see cref="Tokenizer"/> type this runtime Farkle will use.</summary>
    /// <typeparam name="TTokenizer">The type of the new tokenizer. It must have
    /// a public constructor that accepts a <see cref="Grammar"/></typeparam>
    /// <returns>A new runtime Farkle that will parse text with
    /// tokenizers of type <typeparamref name="TTokenizer"/>.</returns>
    /// <exception cref="MissingMethodEsception"><typeparamref name="TTokenizer"/>
    /// does not have a public constructor that accepts a <see cref="Grammar"/>.</exception>
    /// <remarks>A new tokenizer will be created with each parse operation.</remarks>
    member this.ChangeTokenizer<'TTokenizer when 'TTokenizer :> Tokenizer>() =
        this.ChangeTokenizer(TokenizerFactoryOfType typeof<'TTokenizer>)
    interface IGrammarProvider with
        member this.IsBuildSuccessful = this.IsBuildSuccessful
        member this.GetGrammar() = this.GetGrammar()
        member this.GetBuildErrorMessage() = this.GetBuildErrorMessage()

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    /// Changes the post-processor of a `RuntimeFarkle`.
    let changePostProcessor pp rf = {
        Grammar = rf.Grammar
        PostProcessor = pp
        TokenizerFactory = rf.TokenizerFactory
    }

    /// Changes the tokenizer that will be used by this runtime Farkle.
    /// This function accepts a function that takes a `Grammar` and returns a `Tokenizer`.
    let changeTokenizer (fTokenizer: _ -> #Tokenizer) (rf: RuntimeFarkle<'TResult>) =
        rf.ChangeTokenizer
            {new TokenizerFactory() with member _.CreateTokenizer g = fTokenizer g :> _}

    /// Changes the post-processor of a runtime Farkle to a
    /// dummy one suitable for syntax-checking instead of parsing.
    let syntaxCheck rf : [<Nullable(1uy, 2uy)>] _ = changePostProcessor PostProcessors.syntaxCheck rf

    /// Creates a `RuntimeFarkle` from the given grammar and post-processor.
    let create postProcessor (grammar: Grammar) =
        RuntimeFarkle<_>.Create(grammar, postProcessor)

    /// Creates a `RuntimeFarkle` from the given `DesigntimeFarkle&lt;'T&gt;`.
    /// In case there is a problem with the grammar,
    /// the `RuntimeFarkle` will fail every time it is used.
    /// If the designtime Farkle is marked for precompilation and a suitable
    /// precompiled grammar is found, building it again will be avoided.
    let build df =
        let theFabledGrammar = PrecompilerInterface.getGrammarOrBuild df
        let theTriumphantPostProcessor = DesigntimeFarkleBuild.buildPostProcessorOnly df
        RuntimeFarkle<_>.CreateMaybe theTriumphantPostProcessor theFabledGrammar

    /// Creates a syntax-checking `RuntimeFarkle`
    /// from an untyped `DesigntimeFarkle`.
    let buildUntyped df =
        df
        |> PrecompilerInterface.getGrammarOrBuild
        |> RuntimeFarkle<_>.CreateMaybe PostProcessors.syntaxCheck

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    /// Marks the given designtime as available to have its grammar
    /// precompiled ahead of time. See more, including usage restrictions
    /// on https://teo-tsirpanis.github.io/Farkle/the-precompiler.html
    let markForPrecompile df =
        let asm = Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle<_>(df, asm)

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    /// The untyped edition of `markForPrecompile`.
    let markForPrecompileU df =
        let asm = Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle(df, asm)

    /// Parses and post-processes a `CharStream`.
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let parseChars (rf: RuntimeFarkle<'TResult>) input = rf.Parse(input)

    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    let parseMemory rf (input: ReadOnlyMemory<_>) =
        use cs = new CharStream(input)
        parseChars rf cs

    /// Parses and post-processes a string.
    let parseString rf (inputString: string) =
        use cs = new CharStream(inputString)
        parseChars rf cs

    /// Parses and post-processes a .NET `Stream` with the
    /// given character encoding, which may be lazily read.
    /// Better use `parseTextReader` instead.
    [<Obsolete("Streams are supposed to contain binary data; not text. Use parseTextReader instead.")>]
    let parseStream rf doLazyLoad ([<Nullable(2uy)>] encoding: Encoding) (inputStream: Stream) =
        let encoding = if isNull encoding then Encoding.UTF8 else encoding
        use sr = new StreamReader(inputStream, encoding, true, 4096, true)
        use cs =
            match doLazyLoad with
            | true -> new CharStream(sr)
            | false -> new CharStream(sr.ReadToEnd())
        parseChars rf cs

    /// Parses and post-processes a .NET `TextReader`. Its content is lazily read.
    let parseTextReader rf (textReader: TextReader) =
        use cs = new CharStream(textReader, true)
        parseChars rf cs

    /// Parses and post-processes a file at the given path.
    let parseFile rf path =
        use s = File.OpenText(path)
        parseTextReader rf s

    /// Parses and post-processes a string.
    [<Obsolete("Use parseString.")>]
    let parse rf x = parseString rf x

    let internal syntaxCheckerObj =
        unbox<PostProcessor<obj>> PostProcessors.syntaxCheck

open RuntimeFarkle

#nowarn "44"

type RuntimeFarkle<'TResult> with
    /// <summary>Parses and post-processes a
    /// <see cref="ReadOnlyMemory{Char}"/>.</summary>
    /// <param name="mem">The read-only memory to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse mem = parseMemory this mem
    /// <summary>Parses and post-processes a string.</summary>
    /// <param name="str">The string to parse.</param>
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
    member this.Parse(stream, [<Optional; Nullable(2uy)>] encoding, [<Optional; DefaultParameterValue(true)>] doLazyLoad) =
        parseStream this doLazyLoad encoding stream
    /// <summary>Parses and post-processes a <see cref="System.IO.TextReader"/>.</summary>
    /// <param name="textReader">The text reader to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    /// <remarks>The text reader's content will be lazily read.</remarks>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse textReader = parseTextReader this textReader
    /// <summary>Changes the <see cref="PostProcessor"/> of this runtime Farkle.</summary>
    /// <param name="pp">The new post-processor.</param>
    /// <returns>A new runtime Farkle with ite post-
    /// processor changed to <paramref name="pp"/>.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.ChangePostProcessor pp = changePostProcessor pp this
    /// <summary>Parses and post-processes a file.</summary>
    /// <param name="path">The path of the file to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    /// <remarks>The file's content will be lazily read.</remarks>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.ParseFile path = parseFile this path
    /// <summary>Changes the <see cref="PostProcessor"/> of this runtime Farkle to
    /// a dummy one that is useful for syntax-checking instead of parsing.</summary>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.SyntaxCheck() : [<Nullable(1uy, 2uy)>] _ =
        changePostProcessor syntaxCheckerObj this
