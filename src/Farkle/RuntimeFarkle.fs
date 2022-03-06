// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Builder
open Farkle.Common
open Farkle.Grammars
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
            let xs =
                // BuildError.LALRConflictReport is not reported
                // because the LALRConflict errors are sufficient.
                List.filter (function | BuildError.LALRConflictReport _ -> false | _ -> true) xs
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
    PostProcessor: IPostProcessor
    TokenizerFactory: TokenizerFactory
}
with
    static member internal CreateMaybeUntyped postProcessor grammarMaybe: RuntimeFarkle<'TResult> =
        {
            Grammar = grammarMaybe
            PostProcessor = postProcessor
            TokenizerFactory = TokenizerFactory.Default
        }
    static member inline internal CreateMaybe (postProcessor: IPostProcessor<'TResult>) grammarMaybe =
        RuntimeFarkle<'TResult>.CreateMaybeUntyped postProcessor grammarMaybe
    /// <summary>Creates a <see cref="RuntimeFarkle{TResult}"/> from the given
    /// <see cref="Grammar"/> and <see cref="PostProcessor{TResult}"/>.</summary>
    static member Create(grammar, postProcessor: IPostProcessor<'TResult>) =
        grammar
        |> Ok
        |> RuntimeFarkle<'TResult>.CreateMaybe postProcessor

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
        | Error msg ->
            msg
            |> Seq.map string
            |> String.concat Environment.NewLine

    /// <summary>Gets the <see cref="Farkle.Grammars.Grammar"/>
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
                LALRParser.parse grammar this.PostProcessor tokenizer input
                :?> 'TResult
                |> Ok
            with
            | :? ParserException as e -> mkError e.Error
            | :? ParserApplicationException as e ->
                let pos =
                    let pos = &e.Position
                    if pos.HasValue then
                        pos.Value
                    else
                        (input :> ITransformerContext).StartPosition
                ParserError(pos, ParseErrorType.UserError e.Message)
                |> mkError
        | Error x -> Error <| FarkleError.BuildError x
    /// <summary>Changes the <see cref="PostProcessor"/> of this runtime Farkle.</summary>
    /// <param name="pp">The new post-processor.</param>
    /// <returns>A new runtime Farkle with ite post-
    /// processor changed to <paramref name="pp"/>.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.ChangePostProcessor<[<Nullable(0uy)>] 'TNewResult>(pp: IPostProcessor<'TNewResult>) : RuntimeFarkle<'TNewResult> = {
        Grammar = this.Grammar
        TokenizerFactory = this.TokenizerFactory
        PostProcessor = pp
    }
    /// <summary>Changes the runtime Farkle's returning type to
    /// <see cref="Object"/>, without changing its post-processsor.</summary>
    member this.Cast() : [<Nullable(1uy, 0uy)>] RuntimeFarkle<obj> =
        if typeof<'TResult> = typeof<obj> then
            unbox this
        else
            {
                Grammar = this.Grammar
                TokenizerFactory = this.TokenizerFactory
                PostProcessor = this.PostProcessor
            }
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
    let changePostProcessor pp (rf: RuntimeFarkle<'TResult>) = rf.ChangePostProcessor pp

    /// Changes the runtime Farkle's returning type
    /// to object, without changing its post-processor.
    let cast (rf: RuntimeFarkle<'TResult>) = rf.Cast()

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
        RuntimeFarkle<'TResult>.Create(grammar, postProcessor)

    /// Creates a `RuntimeFarkle` from the given `DesigntimeFarkle&lt;'T&gt;`.
    /// In case there is a problem with the grammar, the `RuntimeFarkle` will
    /// fail every time it is used. If the designtime Farkle is marked for
    /// precompilation and a suitable precompiled grammar is found, it will be ignored.
    let build (df: DesigntimeFarkle<'TResult>) =
        let theFabledGrammar, theTriumphantPostProcessor = DesigntimeFarkleBuild.build df
        RuntimeFarkle<_>.CreateMaybe theTriumphantPostProcessor theFabledGrammar

    /// Creates a syntax-checking `RuntimeFarkle` from an untyped `DesigntimeFarkle`.
    /// No transformers or fusers from the designtime Farkle will be executed.
    let buildUntyped df =
        df
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> RuntimeFarkle<_>.CreateMaybe PostProcessors.syntaxCheck

    /// Creates a `RuntimeFarkle` from the given typed `PrecompilableDesigntimeFarkle`.
    /// In case the designtime Farkle has not been precompiled, the `RuntimeFarkle` will
    /// fail every time it is used. The precompiler will run by installing the package
    /// `Farkle.Tools.MSBuild`. Learn more at https://teo-tsirpanis.github.io/Farkle/the-precompiler.html
    let buildPrecompiled (pcdf: PrecompilableDesigntimeFarkle<'a>) =
        let grammar = PrecompilerInterface.buildPrecompiled pcdf
        let pp = pcdf.CreatePostProcessor<'a>()
        RuntimeFarkle<_>.CreateMaybe pp grammar

    /// Creates a syntax-checking `RuntimeFarkle` from the given untyped `PrecompilableDesigntimeFarkle`.
    /// In case the designtime Farkle has not been precompiled, the `RuntimeFarkle` will
    /// fail every time it is used. The precompiler will run by installing the package
    /// `Farkle.Tools.MSBuild`. Learn more at https://teo-tsirpanis.github.io/Farkle/the-precompiler.html
    let buildPrecompiledUntyped pcdf =
        pcdf
        |> PrecompilerInterface.buildPrecompiled
        |> RuntimeFarkle<_>.CreateMaybe PostProcessors.syntaxCheck

    /// Marks the given designtime as available to have its grammar
    /// precompiled ahead of time. Learn more, including usage restrictions
    /// at https://teo-tsirpanis.github.io/Farkle/the-precompiler.html
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let markForPrecompile df =
        let asm = Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle<_>(df, asm)

    /// The untyped edition of `markForPrecompile`.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
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
        nullCheck (nameof inputString) inputString
        use cs = new CharStream(inputString)
        parseChars rf cs

    /// Parses and post-processes a .NET `TextReader`. Its content is lazily read.
    let parseTextReader rf (textReader: TextReader) =
        nullCheck (nameof textReader) textReader
        use cs = new CharStream(textReader, true)
        parseChars rf cs

    /// Parses and post-processes a file at the given path.
    let parseFile rf path =
        nullCheck (nameof path) path
        use s = File.OpenText(path)
        parseTextReader rf s

    let internal syntaxCheckerObj =
        unbox<IPostProcessor<obj>> PostProcessors.syntaxCheck

open RuntimeFarkle

type RuntimeFarkle<'TResult> with
    /// <summary>Parses and post-processes a
    /// <see cref="ReadOnlyMemory{Char}"/>.</summary>
    /// <param name="input">The read-only memory to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse input = parseMemory this input
    /// <summary>Parses and post-processes a string.</summary>
    /// <param name="inputString">The string to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse inputString = parseString this inputString
    /// <summary>Parses and post-processes a <see cref="System.IO.TextReader"/>.</summary>
    /// <param name="textReader">The text reader to parse.</param>
    /// <returns>An F# result type containing either the
    /// post-processed return type, or a type describing
    /// what did wrong and where.</returns>
    /// <remarks>The text reader's content will be lazily read.</remarks>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Parse textReader = parseTextReader this textReader
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
