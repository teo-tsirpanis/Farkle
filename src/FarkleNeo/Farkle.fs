// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// This file contains the F# API of Farkle 7+.
// It is distributed as a source file, to avoid the main Farkle
// library depending on FSharp.Core. All members of this file
// must be declared as non-public, and all trivial functions must
// be declared as inline.

namespace Farkle.Grammars

/// Functions to create grammars.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Grammar =

    open System
    open System.Collections.Immutable
    open System.IO

    /// Creates a grammar from a read-only span of bytes.
    let inline ofSpan (x: ReadOnlySpan<byte>) = Grammar.Create x

    /// Creates a grammar from an immutable array of bytes.
    /// Should be preferred over ofSpan when an immutable array is available.
    let inline ofBytes (x: ImmutableArray<byte>) = Grammar.Create x

    let inline ofFile path = Grammar.CreateFromFile path

    /// Converts a GOLD Parser grammar to a Farkle grammar.
    let inline ofGoldParserStream (x: Stream) = Grammar.CreateFromGoldParserGrammar x

namespace Farkle.Parser.Tokenizers

open Farkle.Grammars

/// Represents a component of a tokenizer chain.
/// A list of objects of this type can be used to change the tokenizer of a CharParser.
/// This type provides an idiomatic F# API for the
/// Farkle.Parser.Tokenizers.ChainedTokenizerBuilder type.
/// See the documentation of that type for more information.
type internal ChainedTokenizerComponent<'TChar> =
    /// The parser's existing tokenizer.
    | DefaultTokenizer
    /// A tokenizer object.
    | TokenizerObject of Tokenizer<'TChar>
    /// A function that requires grammar-specific information.
    | TokenizerFactory of (IGrammarProvider -> Tokenizer<'TChar>)

namespace Farkle.Parser.Tokenizers.Internal

open System.ComponentModel

/// Provides internal functions for use by Farkle's F# API.
/// Should not be used by user code. No compatibility guarantees
/// are provided for this module.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal ChainedTokenizerBuilder =

    open Farkle.Parser.Tokenizers

    let create<'TChar> components =
        components
        |> List.fold
            (fun (builder: ChainedTokenizerBuilder<'TChar> voption) x ->
                match builder, x with
                | ValueSome builder, DefaultTokenizer -> builder.AddDefault()
                | ValueSome builder, TokenizerObject tokenizer -> builder.Add tokenizer
                | ValueSome builder, TokenizerFactory tokenizerFactory -> builder.Add tokenizerFactory
                | ValueNone, DefaultTokenizer -> ChainedTokenizerBuilder<'TChar>.CreateDefault()
                | ValueNone, TokenizerObject tokenizer -> ChainedTokenizerBuilder<'TChar>.Create tokenizer
                | ValueNone, TokenizerFactory tokenizerFactory -> ChainedTokenizerBuilder<'TChar>.Create tokenizerFactory
                |> ValueSome
            )
            ValueNone
        |> function
        | ValueSome builder -> builder
        | ValueNone -> invalidArg (nameof components) "The components list must not be empty."

namespace Farkle

open Farkle.Parser
open System
open System.IO

/// Functions to work with Farkle parser results.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal ParserResult =

    /// Converts a Farkle parser result to an F# result.
    let inline toResult (x: ParserResult<_>) =
        if x.IsSuccess then
            Ok x.Value
        else
            Error x.Error

/// Contains active patterns for types in the Farkle namespace.
/// This module is automatically opened.
[<AutoOpen>]
module internal ActivePatterns =

    let inline (|ParserSuccess|ParserError|) (x: ParserResult<_>) =
        if x.IsSuccess then
            ParserSuccess x.Value
        else
            ParserError x.Error

/// Functions to create and modify CharParser objects, as well as parse text with them.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal CharParser =

    open Farkle.Parser.Semantics
    open Farkle.Parser.Tokenizers

    /// Creates a CharParser from a grammar and a semantic provider.
    let inline create semanticProvider grammar = CharParser.Create<'T>(grammar, semanticProvider)

    /// Creates a CharParser that performs no semantic actions
    /// and returns unit if successful.
    let inline createSyntaxCheck grammar = CharParser.CreateSyntaxChecker<unit>(grammar)

    /// Converts a CharParser to one that performs no semantic actions
    /// and returns unit if successful.
    let inline syntaxCheck parser = CharParser.ToSyntaxChecker<'T,unit> parser

    /// Changes the semantic provider of a CharParser.
    let inline withSemanticProvider (semanticProvider: ISemanticProvider<char, 'TNew>) (parser: CharParser<'T>) =
        parser.WithSemanticProvider semanticProvider

    /// Changes the semantic provider of a CharParser
    /// to one that requires grammar-specific information.
    let inline withSemanticProviderFactory semanticProviderFactory (parser: CharParser<'T>) =
        Func<_,_> semanticProviderFactory
        |> parser.WithSemanticProvider<'TNew>

    /// Changes the tokenizer of a CharParser.
    let inline withTokenizer (tokenizer: Tokenizer<char>) (parser: CharParser<'T>) = parser.WithTokenizer tokenizer

    /// Changes the tokenizer of a CharParser
    /// to one that requires grammar-specific information.
    let inline withTokenizerFactory tokenizerFactory (parser: CharParser<'T>) =
        Func<_,_> tokenizerFactory
        |> parser.WithTokenizer

    /// Changes the tokenizer of a CharParser to a tokenizer chain
    /// specified by a list of components.
    /// This type provides an idiomatic F# API for the
    /// Farkle.Parser.Tokenizers.ChainedTokenizerBuilder type.
    /// See the documentation of that type for more information.
    let inline withTokenizerChain components (parser: CharParser<'T>) =
        components
        |> Internal.ChainedTokenizerBuilder.create
        |> parser.WithTokenizer

    /// Parses a read-only span of characters. All types of characters are supported.
    let inline parseSpan (parser: IParser<char, 'T>) (x: ReadOnlySpan<_>) = parser.Parse x

    /// Parses a string.
    let inline parseString (parser: IParser<_, 'T>) (x: string) = parser.Parse x

    /// Parses characters from a text reader.
    let inline parseTextReader (parser: IParser<_, 'T>) (x: TextReader) = parser.Parse x

    /// Parses characters from a file.
    let inline parseFile (parser: IParser<_, 'T>) x = parser.ParseFile x

    /// Asynchronously parses characters from a text reader.
    let asyncParseTextReader (parser: IParser<_, 'T>) x = async {
        let! ct = Async.CancellationToken
        let vt = parser.ParseAsync(x, ct)
        if vt.IsCompleted then
            return vt.Result
        else
            return! vt.AsTask() |> Async.AwaitTask
    }

    /// Asynchronously parses characters from a file.
    let asyncParseFile (parser: IParser<_, 'T>) x = async {
        let! ct = Async.CancellationToken
        let vt = parser.ParseFileAsync(x, ct)
        if vt.IsCompleted then
            return vt.Result
        else
            return! vt.AsTask() |> Async.AwaitTask
    }

namespace Farkle.Diagnostics

/// Contains active patterns for types in the Farkle.Diagnostics namespace.
/// This module is automatically opened.
[<AutoOpen>]
module internal ActivePatterns =

    open System.Collections.Immutable

    let private mapExpectedTokenNames (x: string ImmutableArray) =
        let b = ImmutableArray.CreateBuilder(x.Length)
        for i = 0 to x.Length - 1 do
            b.Add <| ValueOption.ofObj x.[i]
        b.MoveToImmutable()

    let inline (|ParserDiagnostic|_|) (x: obj) =
        match x with
        | :? ParserDiagnostic as x -> Some(x.Position, x.Message)
        | _ -> None

    let (|SyntaxError|_|) (x: obj) =
        match x with
        | :? SyntaxError as x -> Some(mapExpectedTokenNames x.ExpectedTokenNames, ValueOption.ofObj x.ActualTokenName)
        | _ -> None

    let inline (|LexicalError|_|) (x: obj) =
        match x with
        | :? LexicalError as x -> Some <| ValueOption.ofObj x.TokenText
        | _ -> None

namespace Farkle.Builder

open System

/// F#-friendly members of the `Farkle.Builder.Regex` class.
/// Please consult the members of the class for documentation.
module Regex =

    open System.Collections.Immutable

    let private makeImmutableArray<'T,'TSeq when 'TSeq :> 'T seq> (x: 'TSeq) =
        if Type.op_Equality(typeof<'TSeq>, typeof<ImmutableArray<'T>>) then
            Unchecked.unbox x
        else
            x.ToImmutableArray()

    /// An alias for `Regex.Literal` that takes a character.
    let char (c: char) = Regex.Literal c

    /// An alias for `Regex.OneOf` that takes characters.
    let chars (str: #seq<char>) =
        str
        |> makeImmutableArray
        |> Regex.OneOf

    /// An alias for `Regex.OneOf` that takes character ranges.
    let charRanges (str: #seq<struct(char * char)>) =
        str
        |> makeImmutableArray
        |> Regex.OneOf

    /// An alias for `Regex.Any`.
    let any = Regex.Any

    /// An alias for `Regex.NotOneOf` that takes characters.
    let allButChars  (str: #seq<char>) =
        str
        |> makeImmutableArray
        |> Regex.NotOneOf

    /// An alias for `Regex.NotOneOf` that takes character ranges.
    let allButCharRanges (str: #seq<struct(char * char)>) =
        str
        |> makeImmutableArray
        |> Regex.NotOneOf

    /// An alias for `Regex.Literal` that takes a string.
    let string (str: string) = Regex.Literal str

    /// An alias for `Regex.Join`.
    let concat (xs: #seq<Regex>) = Regex.Join(makeImmutableArray xs)

    /// An alias for `Regex.Choice`.
    let choice (xs: #seq<Regex>) = Regex.Choice(makeImmutableArray xs)

    /// An alias for `Regex.Repeat`.
    let repeat num (x: Regex) = x.Repeat num

    /// An alias for `Regex.Optional`.
    let optional (x: Regex) = x.Optional()

    /// An alias for `Regex.Between`.
    let between from upTo (x: Regex) = x.Between(from, upTo)

    /// An alias for `Regex.AtLeast`.
    let atLeast num (x: Regex) = x.AtLeast num

    /// An alias for `Regex.ZeroOrMore`.
    /// The name alludes to the Kleene Star.
    let star (x: Regex) = x.ZeroOrMore()

    /// An alias for `atLeast 1`.
    /// The name alludes to the plus symbol of regular expressions.
    let plus x = atLeast 1 x

    /// An alias for `Regex.FromRegexString`.
    let regexString x = Regex.FromRegexString x

/// F# operators to easily create `Regex`es.
[<AutoOpen>]
module RegexCompatibilityOperators =

    /// Obsolete operator for Farkle 6 compatibility.
    [<Obsolete("The <&> operator on regexes is obsolete. Use the + operator or the concat function instead.")>]
    let (<&>) (x1: Regex) x2 = x1 + x2

    /// Obsolete operator for Farkle 6 compatibility.
    [<Obsolete("The <|> operator on regexes is obsolete. Use the ||| operator or the choice function instead.")>]
    let (<|>) (x1: Regex) x2 = x1 ||| x2

namespace Farkle

open System

// -------------------------------------------------------------------
// Farkle 6 compatibility APIs
// -------------------------------------------------------------------

[<Obsolete("Use CharParser<'T> instead")>]
type internal RuntimeFarkle<'TResult> = CharParser<'TResult>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal RuntimeFarkle =

    open Farkle.Parser.Tokenizers

    [<Obsolete("Use CharParser.withSemanticProvicer instead")>]
    let inline changePostProcessor pp rf = CharParser.withSemanticProvider pp rf

    [<Obsolete("Use CharParser.create instead")>]
    let inline create semanticProvider grammar = CharParser.create semanticProvider grammar

    [<Obsolete("Use CharParser.withTokenizer instead. Also note that the API for \
customizing tokenizers has substantially changed in Farkle 7.")>]
    let inline changeTokenizer (fTokenizer: _ -> #Tokenizer<char>) parser =
        parser
        |> CharParser.withTokenizerFactory (fun x -> x.GetGrammar() |> fTokenizer :> _)

    [<Obsolete("Use CharParser.syntaxCheck instead.")>]
    let inline syntaxCheck rf = CharParser.syntaxCheck rf

    [<Obsolete("Use CharParser.parseSpan instead.")>]
    let inline parseMemory rf (x: ReadOnlyMemory<char>) = CharParser.parseSpan rf (x.Span) |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseString instead.")>]
    let inline parseString rf input = CharParser.parseString rf input |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseTextReader instead.")>]
    let inline parseTextReader rf input = CharParser.parseTextReader rf input |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseFile instead.")>]
    let inline parseFile rf input = CharParser.parseFile rf input |> ParserResult.toResult
