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
open System.Text

/// A type signifying an error during the parsing process.
[<RequireQualifiedAccess>]
type FarkleError =
    /// There was a parsing error.
    | ParseError of Message<ParseErrorType>
    /// There had been an error while building the grammar .
    | BuildError of BuildError
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | BuildError x -> sprintf "Error while building the grammar: %O" x

/// A reusable parser and post-processor, created for a specific grammar, and returning
/// a specific type of object that best describes an expression of the language of this grammar.
[<NoComparison; ReferenceEquality>]
type RuntimeFarkle<'TResult> = internal {
    Grammar: Result<Grammar * OptimizedOperations,BuildError>
    PostProcessor: PostProcessor<'TResult>
}
with
    static member internal CreateMaybe postProcessor grammarMaybe =
        let grammarMaybe =
            grammarMaybe
            |> Result.map (fun g -> g, OptimizedOperations.Create g)
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

    /// Returns a user-friendly error message that
    /// describes what had gone wrong while building,
    /// or an empty string if building had been successful.
    member this.GetBuildErrorMessage() =
        match this.Grammar with
        | Ok _ -> String.Empty
        | Error msg -> string msg

    [<Obsolete("This method will be removed in a future release. \
Use a conjunction of IsBuildSuccessful, GetBuildErrorMessage and GetGrammar.")>]
    /// <summary>Gets the <see cref="Farkle.Grammar.Grammar"/>
    /// behind the <see cref="RuntimeFarkle{TResult}"/>, wrapped
    /// in an F# result type.</summary>
    member this.TryGetGrammar() = Result.map fst this.Grammar

    /// <summary>Gets the <see cref="Farkle.Grammar.Grammar"/>
    /// behind the <see cref="RuntimeFarkle{TResult}"/>.</summary>
    /// <exception cref="System.Exception">Building the grammar
    /// had failed. The exception's message contains further details.</exception>
    member this.GetGrammar() =
        match this.Grammar with
        | Ok (grammar, _) -> grammar
        | Error msg -> msg |> string |> failwith

/// Some `PostProcessor`s, reusable and ready to use.
module PostProcessors =

    [<CompiledName("SyntacChecker")>]
    /// This post-processor does not return anything meaningful to its consumer.
    /// It is useful for checking the syntax of a string with respect to a grammar.
    let syntaxCheck =
        {new PostProcessor<unit> with
            member __.Transform (_, _, _) = null
            member __.Fuse (_, _) = null}

    [<CompiledName("AST")>]
    /// This post-processor creates a domain-ignorant `AST`.
    let ast =
        {new PostProcessor<AST> with
            member __.Transform (sym, pos, x) = AST.Content(sym, pos, x.ToString()) |> box
            member __.Fuse (prod, items) = AST.Nonterminal(prod, items |> Seq.take prod.Handle.Length |> Seq.cast |> List.ofSeq) |> box}

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = {
        Grammar = rf.Grammar
        PostProcessor = pp
    }

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

    /// Creates a `RuntimeFarkle` from the given `DesigntimeFarkle<T>`.
    /// In case there is a problem with the grammar,
    /// the `RuntimeFarkle` will fail every time it is used.
    /// If the designtime Farkle is marked for precompilation and a suitable
    /// precompiled grammar is found, building it again will be avoided.
    [<CompiledName("Build")>]
    let build df =
        let theFabledGrammar = Precompiler.buildGrammar df
        let theTriumphantPostProcessor = DesigntimeFarkleBuild.buildPostProcessorOnly df
        RuntimeFarkle<_>.CreateMaybe theTriumphantPostProcessor theFabledGrammar

    /// Creates a syntax-checking `RuntimeFarkle`
    /// from an untyped `DesigntimeFarkle`.
    [<CompiledName("BuildUntyped")>]
    let buildUntyped df =
        df
        |> Precompiler.buildGrammar
        |> RuntimeFarkle<_>.CreateMaybe PostProcessors.syntaxCheck

    [<CompiledName("MarkForPrecompile")>]
    /// Marks the given designtime as available to have its grammar
    /// precompiled ahead of time. Besides performance improvements,
    /// precompiling a grammar reports any errors at compile-time
    /// instead of when a string is going to be parsed.
    /// For this function to have effect, it has to be applied to the
    /// topmost designtime Farkle that is stored in a read-only static
    /// field (like a let-cound value in a module). Untyped designtime
    /// Farkles can use DesigntimeFarkle.cast and then cast back to
    /// the untyped designtime Farkle.
    let markForPrecompile df =
        let asm = Assembly.GetCallingAssembly()
        Precompiler.PrecompilableDesigntimeFarkle<_>(df, asm) :> DesigntimeFarkle<_>

    /// Parses and post-processes a `CharStream`.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let parseChars (rf: RuntimeFarkle<'TResult>) fMessage input =
        match rf.Grammar with
        | Ok (grammar, oops) ->
            let fTokenize = Tokenizer.tokenize grammar.Groups grammar.DFAStates oops rf.PostProcessor fMessage
            try
                LALRParser.parseLALR fMessage grammar.LALRStates oops rf.PostProcessor fTokenize input |> Ok
            with
            | ParseError msg -> msg |> FarkleError.ParseError |> Error
        | Error x -> Error <| FarkleError.BuildError x

    [<CompiledName("ParseMemory")>]
    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    /// This function also accepts a custom parse message handler.
    let parseMemory rf fMessage input =
        input |> CharStream.ofReadOnlyMemory |> parseChars rf fMessage

    /// Parses and post-processes a string.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseString")>]
    let parseString rf fMessage inputString =
        inputString |> CharStream.ofString |> parseChars rf fMessage

    /// Parses and post-processes a .NET `Stream` with the
    /// given character encoding, which may be lazily read.
    /// Better use `parseTextReader` instead.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseStream"); Obsolete("Streams are supposed to \
contain binary data; not text. Use parseTextReader instead.")>]
    let parseStream rf fMessage doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        use sr = new StreamReader(inputStream, encoding, true, 4096, true)
        use cs =
            match doLazyLoad with
            | true -> CharStream.ofTextReader sr
            | false -> sr.ReadToEnd() |> CharStream.ofString
        parseChars rf fMessage cs

    /// Parses and post-processes a .NET `TextReader`. Its content is lazily read.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseTextReader")>]
    let parseTextReader rf fMessage textReader =
        let cs = CharStream.ofTextReader textReader
        parseChars rf fMessage cs

    /// Parses and post-processes a file at the given path with the given character encoding.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseFile")>]
    let parseFile rf fMessage inputFile =
        use s = File.OpenText(inputFile)
        parseTextReader rf fMessage s

    /// Parses and post-processes a string.
    // This function was inspired by FParsec, which has some "runParserOn***" functions,
    // and the simple and easy-to-use function named "run", that just parses a string.
    [<CompiledName("Parse")>]
    let parse rf x = parseString rf ignore x
