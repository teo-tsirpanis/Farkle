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
            b.Add <| ValueOption.ofObj x[i]
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

namespace Farkle.Diagnostics.Builder

/// Contains active patterns for types in the Farkle.Diagnostics.Builder namespace.
/// This module is automatically opened.
[<AutoOpen>]
module internal ActivePatterns =

    let inline (|IndistinguishableSymbolsError|_|) (x: obj) =
        match x with
        | :? IndistinguishableSymbolsError as x -> Some x.SymbolNames
        | _ -> None

namespace Farkle.Builder

open System
open System.Collections
open System.Collections.Generic

module private Internal =

    open System.Collections.Immutable

    let inline makeImmutableArray<'T,'TSeq when 'TSeq :> 'T seq> (x: 'TSeq) =
        if Type.op_Equality(typeof<'TSeq>, typeof<ImmutableArray<'T>>) then
            Unchecked.unbox x
        else
            x.ToImmutableArray()

/// F#-friendly members of the `Farkle.Builder.Regex` class.
/// Please consult the members of the class for documentation.
module internal Regex =

    open Internal

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

    /// An alias for `Regex.CaseSensitive`.
    let caseSensitive (x: Regex) = x.CaseSensitive()

    /// An alias for `Regex.CaseInsensitive`.
    let caseInsensitive (x: Regex) = x.CaseInsensitive()

/// F# operators to easily create `Regex`es.
[<AutoOpen>]
module internal RegexCompatibilityOperators =

    /// Obsolete operator for Farkle 6 compatibility.
    [<Obsolete("The <&> operator on regexes is obsolete. Use the + operator or the concat function instead.")>]
    let (<&>) (x1: Regex) x2 = x1 + x2

    /// Obsolete operator for Farkle 6 compatibility.
    [<Obsolete("The <|> operator on regexes is obsolete. Use the ||| operator or the choice function instead.")>]
    let (<|>) (x1: Regex) x2 = x1 ||| x2

/// An alias to the `Farkle.Builder.Transformer` class.
/// It is provided with a one-letter name to make it easier to type
/// since it is required in F#.
type internal T<'TChar, 'T> = Transformer<'TChar, 'T>

/// Internal facade types for production builders to work around limitations of F#.
/// Types in this module must not be used directly by user code.
// This module cannot be made internal because of https://github.com/dotnet/fsharp/issues/16762
[<CompiledName("InternalFSharpProductionBuilders")>]
module FSharpProductionBuilders =

    /// Helper interface to enable the `prec` function. Do not use directly.
    type ISupportPrecedence<'T> =
        abstract member WithPrecedence: obj -> 'T

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder<'T1,'T2,'T3,'T4,'T5>(pb: ProductionBuilders.ProductionBuilder<'T1,'T2,'T3,'T4,'T5>) =
        member private _.Value = pb
        static member (.>>) (pb: ProductionBuilder<_,_,_,_,_>, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder<_,_,_,_,_>
        static member (.>>) (pb: ProductionBuilder<_,_,_,_,_>, str: string) = pb.Value.Append str |> ProductionBuilder<_,_,_,_,_>
        static member (=>) (pb: ProductionBuilder<_,_,_,_,_>, f: _ -> _ -> _ -> _ -> _ -> _) =
            let f = OptimizedClosures.FSharpFunc<_,_,_,_,_,_>.Adapt f
            Func<_,_,_,_,_,_>(f.Invoke) |> pb.Value.Finish
        interface ISupportPrecedence<ProductionBuilder<'T1,'T2,'T3,'T4,'T5>> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder<_,_,_,_,_>

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder<'T1,'T2,'T3,'T4>(pb: ProductionBuilders.ProductionBuilder<'T1,'T2,'T3,'T4>) =
        member private _.Value = pb
        static member (.>>) (pb: ProductionBuilder<_,_,_,_>, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder<_,_,_,_>
        static member (.>>) (pb: ProductionBuilder<_,_,_,_>, str: string) = pb.Value.Append str |> ProductionBuilder<_,_,_,_>
        static member (.>>.) (pb: ProductionBuilder<_,_,_,_>, symbol) = pb.Value.Extend symbol |> ProductionBuilder<_,_,_,_,_>
        static member (=>) (pb: ProductionBuilder<_,_,_,_>, f: _ -> _ -> _ -> _ -> _) =
            let f = OptimizedClosures.FSharpFunc<_,_,_,_,_>.Adapt f
            Func<_,_,_,_,_>(f.Invoke) |> pb.Value.Finish
        interface ISupportPrecedence<ProductionBuilder<'T1,'T2,'T3,'T4>> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder<_,_,_,_>

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder<'T1,'T2,'T3>(pb: ProductionBuilders.ProductionBuilder<'T1,'T2,'T3>) =
        member private _.Value = pb
        static member (.>>) (pb: ProductionBuilder<_,_,_>, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder<_,_,_>
        static member (.>>) (pb: ProductionBuilder<_,_,_>, str: string) = pb.Value.Append str |> ProductionBuilder<_,_,_>
        static member (.>>.) (pb: ProductionBuilder<_,_,_>, symbol) = pb.Value.Extend symbol |> ProductionBuilder<_,_,_,_>
        static member (=>) (pb: ProductionBuilder<_,_,_>, f: _ -> _ -> _ -> _) =
            let f = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt f
            Func<_,_,_,_>(f.Invoke) |> pb.Value.Finish
        interface ISupportPrecedence<ProductionBuilder<'T1,'T2,'T3>> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder<_,_,_>

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder<'T1,'T2>(pb: ProductionBuilders.ProductionBuilder<'T1,'T2>) =
        member private _.Value = pb
        static member (.>>) (pb: ProductionBuilder<_,_>, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder<_,_>
        static member (.>>) (pb: ProductionBuilder<_,_>, str: string) = pb.Value.Append str |> ProductionBuilder<_,_>
        static member (.>>.) (pb: ProductionBuilder<_,_>, symbol) = pb.Value.Extend symbol |> ProductionBuilder<_,_,_>
        static member (=>) (pb: ProductionBuilder<_,_>, f: _ -> _ -> _) =
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt f
            Func<_,_,_>(f.Invoke) |> pb.Value.Finish
        interface ISupportPrecedence<ProductionBuilder<'T1,'T2>> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder<_,_>

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder<'T1>(pb: ProductionBuilders.ProductionBuilder<'T1>) =
        member private _.Value = pb
        member x.AsProduction() = x.Value.AsProduction()
        static member (.>>) (pb: ProductionBuilder<_>, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder<_>
        static member (.>>) (pb: ProductionBuilder<_>, str: string) = pb.Value.Append str |> ProductionBuilder<_>
        static member (.>>.) (pb: ProductionBuilder<_>, symbol) = pb.Value.Extend symbol |> ProductionBuilder<_,_>
        static member (=>) (pb: ProductionBuilder<_>, f: _ -> _) =
            Func<_,_>(f) |> pb.Value.Finish
        interface ISupportPrecedence<ProductionBuilder<'T1>> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder<_>

    /// Wraps a production builder to provide F# operators. Do not use directly.
    [<Struct>]
    type ProductionBuilder(pb: Farkle.Builder.ProductionBuilder) =
        member private _.Value = pb
        static member Empty = Farkle.Builder.ProductionBuilder.Empty |> ProductionBuilder
        static member (.>>) (pb: ProductionBuilder, symbol: IGrammarSymbol) = pb.Value.Append symbol |> ProductionBuilder
        static member (.>>) (pb: ProductionBuilder, str: string) = pb.Value.Append str |> ProductionBuilder
        static member (.>>.) (pb: ProductionBuilder, symbol) = pb.Value.Extend symbol |> ProductionBuilder<_>
        static member (=>) (pb: ProductionBuilder, f: unit -> _) =
            Func<_>(f) |> pb.Value.Finish
        static member (=%) (pb: ProductionBuilder, x) = pb.Value.FinishConstant x
        /// Implicit conversion to the real production builder type.
        /// Required for seamless interoperability between the two types.
        static member op_Implicit (pb: ProductionBuilder) = pb.Value
        /// Implicit conversion from the real production builder type.
        /// Required for seamless interoperability between the two types.
        static member op_Implicit (pb: Farkle.Builder.ProductionBuilder) = ProductionBuilder(pb)
        interface ISupportPrecedence<ProductionBuilder> with
            member this.WithPrecedence token = this.Value.WithPrecedence token |> ProductionBuilder

/// Functions to work with IGrammarBuilder objects.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal GrammarBuilder =

    /// Builds a grammar and returns a parser.
    let build (x: IGrammarBuilder<_>) = x.Build()

    /// Builds a grammar and returns a syntax checker.
    /// Syntax checkers do not perform semantic actions.
    let buildSyntaxCheck (x: IGrammarBuilder) = x.BuildSyntaxCheck<unit>()

/// Functions to set options on grammar symbols.
/// To set options on the entire grammar, use the extension methods on grammar builders.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal GrammarSymbol =

    /// Renames a grammar symbol. This is an alias for `GrammarSymbolExtensions.Rename`.
    let inline rename name (symbol: IGrammarSymbol<_>) = symbol.Rename name

    /// Renames an untyped grammar symbol. This is an alias for `GrammarSymbolExtensions.Rename`.
    let inline renameU name (symbol: IGrammarSymbol) = symbol.Rename name

/// F# operators and functions to easily work with grammar symbols and productions.
/// Production builders created by F# operators cannot be used from C# and vice versa,
/// and support only up to five significant grammar symbols.
/// This module is automatically opened.
[<AutoOpen>]
module internal GrammarBuilderOperators =

    open System.Collections.Immutable

    type private ListBuilder<'T>() =
        let mutable builder = CompilerServices.ListCollector<_>()
        member _.MoveToList() = builder.Close()
        interface IEnumerable with
            member _.GetEnumerator() = raise (NotImplementedException())
        interface IEnumerable<'T> with
            member _.GetEnumerator() = raise (NotImplementedException())
        interface ICollection<'T> with
            member _.Count = raise (NotImplementedException())
            member _.IsReadOnly = false
            member _.Add x = builder.Add x
            member _.Clear() = builder <- Unchecked.defaultof<_>
            member _.Contains _ = raise (NotImplementedException())
            member _.CopyTo(_, _) = raise (NotImplementedException())
            member _.Remove _ = raise (NotImplementedException())

    type Untyped.Nonterminal with
        /// An alias for `SetProductions`.
        member inline x.SetProductions ([<ParamArray>] productions: FSharpProductionBuilders.ProductionBuilder[]) =
            let b = ImmutableArray.CreateBuilder<ProductionBuilder>(productions.Length)
            for i = 0 to productions.Length - 1 do
                b.Add(productions[i])
            x.SetProductions(b.MoveToImmutable())

    let private symbolName (symbol: IGrammarSymbol) = symbol.Name

    /// Creates a terminal.
    let inline terminal name (fTransform: T<_,_>) regex = Terminal.Create(name, regex, fTransform)

    /// Creates a terminal that does not perform any semantic actions.
    let inline terminalU name regex = Terminal.Create(name, regex)

    /// Creates a terminal that is never produced by Farkle's default tokenizer.
    /// Users will have to provide a custom tokenizer to match it.
    let inline virtualTerminal name = Terminal.Virtual(name)

    /// Creates a terminal that matches a literal string.
    let inline literal str = Terminal.Literal str

    /// An alias for `Terminal.NewLine`.
    let inline newline<'a> = Terminal.NewLine

    /// Creates a `Nonterminal` whose productions must be
    /// later set with `SetProductions`, or it will raise an
    /// error on building. Useful for recursive productions.
    let inline nonterminal name = Nonterminal.Create name

    /// Creates an `Untyped.Nonterminal` whose productions must be
    /// later set with `SetProductions`, or it will raise an
    /// error on building. Useful for recursive productions.
    let inline nonterminalU name = Nonterminal.CreateUntyped name

    /// Creates a `IGrammarSymbol&lt;'T&gt;` that represents
    /// a nonterminal with the given name and productions.
    let inline (||=) name (productions: #seq<_>) =
        Nonterminal.Create(name, Internal.makeImmutableArray productions)

    /// Creates an `IGrammarSymbol&lt;'T&gt;` that represents
    /// a nonterminal with the given name and productions.
    let (|||=) name (productions: seq<FSharpProductionBuilders.ProductionBuilder>) =
        let productions =
            productions
            |> Seq.map FSharpProductionBuilders.ProductionBuilder.op_Implicit
            |> ImmutableArray.CreateRange
        Nonterminal.CreateUntyped(name, productions)

    /// An empty production builder.
    let inline empty<'a> = FSharpProductionBuilders.ProductionBuilder.Empty

    /// Sets a precedence token on a production builder.
    let inline prec<'T when 'T :> FSharpProductionBuilders.ISupportPrecedence<'T>> (token: obj) (pb: 'T) =
        pb.WithPrecedence token

    /// Finishes building a production, making it return its single significant member unchanged.
    let inline asProduction (pb: FSharpProductionBuilders.ProductionBuilder<_>) = pb.AsProduction()

    /// Obsolete, use `asProduction` instead.
    [<Obsolete("Use the asProduction function instead.")>]
    let inline asIs pb = asProduction pb

    /// Starts a production builder with a symbol as a significant member.
    let inline (!@) (symbol: IGrammarSymbol<_>) = empty .>>. symbol

    /// Starts a production builder with a symbol.
    let inline (!%) (symbol: IGrammarSymbol) = empty .>> symbol

    /// Starts a production builder with a literal.
    let inline (!&) (str : string) = empty .>> str

    /// Creates a symbol that recognizes many occurrences
    /// of the given one and returns them in any collection type.
    let inline manyCollection<'T, 'TCollection
            when 'TCollection :> ICollection<'T>
            and 'TCollection: (new: unit -> 'TCollection)> (symbol: IGrammarSymbol<'T>) =
        symbol.Many<'T, 'TCollection>(false)

    /// Creates a symbol that recognizes more than one occurrences
    /// of the given one and returns them in any collection type.
    let inline manyCollection1<'T, 'TCollection
            when 'TCollection :> ICollection<'T>
            and 'TCollection: (new: unit -> 'TCollection)> (symbol: IGrammarSymbol<'T>) =
        symbol.Many<'T, 'TCollection>(true)

    /// Creates a symbol that recognizes many occurrences
    /// of the given one and returns them in a list.
    let many symbol =
        $"{symbolName symbol} List"
        ||= [!@ (manyCollection<_, ListBuilder<_>> symbol) => (fun x -> x.MoveToList())]

    /// Creates a symbol that recognizes more than one occurrences
    /// of the given one and returns them in a list.
    let many1 symbol =
        $"{symbolName symbol} Non-empty List"
        ||= [!@ (manyCollection1<_, ListBuilder<_>> symbol) => (fun x -> x.MoveToList())]

    /// Creates a symbol that recognizes more than one occurrences
    /// of `symbol` separated by `separator` and returns them in any collection type.
    let inline sepByCollection<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)> separator (symbol: IGrammarSymbol<'T>) =
        symbol.SeparatedBy<'T, 'TCollection>(separator, false)

    /// Creates a symbol that recognizes many occurrences of
    /// `symbol` separated by `separator` and returns them in any collection type.
    let inline sepByCollection1<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)> separator (symbol: IGrammarSymbol<'T>) =
        symbol.SeparatedBy<'T, 'TCollection>(separator, true)

    /// Creates a symbol that recognizes more than one
    /// occurrences of `symbol` separated by `separator` and returns them in a list.
    let sepBy separator symbol =
        $"{symbolName symbol} List Separated By {symbolName separator}" ||=
        [!@ (sepByCollection<_, ListBuilder<_>> separator symbol) => (fun x -> x.MoveToList())]

    /// Creates a symbol that recognizes many occurrences
    /// of `symbol` separated by `separator` and returns them in a list.
    let sepBy1 separator symbol =
        $"{symbolName symbol} Non-Empty List Separated By {symbolName separator}" ||=
        [!@ (sepByCollection1<_, ListBuilder<_>> separator symbol) => (fun x -> x.MoveToList())]

    /// Creates a symbol that matches either one occurrence of `symbol` or none.
    /// In the latter case it returns `None`.
    let opt symbol =
        $"{symbolName symbol} Maybe" ||= [
            !@ symbol => Some
            empty =% None
        ]

    /// Creates a symbol that matches either one occurrence of `symbol` or none.
    /// In the latter case it returns `ValueNone`.
    let vopt symbol =
        $"{symbolName symbol} Maybe" ||= [
            !@ symbol => ValueSome
            empty =% ValueNone
        ]

    /// Creates a symbol that transforms the output of `symbol with `f`.
    let (|>>) (f: _ -> 'b) symbol =
        $"{symbolName symbol} :?> {typeof<'b>.Name}" ||= [!@ symbol => f]

// -------------------------------------------------------------------
// Farkle 6 compatibility APIs
// -------------------------------------------------------------------

namespace Farkle

open System

[<Obsolete("Use CharParser<'T> instead.")>]
type internal RuntimeFarkle<'TResult> = CharParser<'TResult>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal RuntimeFarkle =

    open Farkle.Builder
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
    let inline parseMemory rf (x: ReadOnlyMemory<char>) = CharParser.parseSpan rf x.Span |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseString instead.")>]
    let inline parseString rf input = CharParser.parseString rf input |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseTextReader instead.")>]
    let inline parseTextReader rf input = CharParser.parseTextReader rf input |> ParserResult.toResult

    [<Obsolete("Use CharParser.parseFile instead.")>]
    let inline parseFile rf input = CharParser.parseFile rf input |> ParserResult.toResult

    [<Obsolete("Use GrammarBuilder.build instead.")>]
    let inline build df = GrammarBuilder.build df

    [<Obsolete("Use GrammarBuilder.buildSyntaxCheck instead.")>]
    let inline buildUntyped df = GrammarBuilder.buildSyntaxCheck df

namespace Farkle.Builder

open System

[<Obsolete("Use IGrammarSymbol for individual grammar symbols or IGrammarBuilder for whole grammars instead.")>]
type internal DesigntimeFarkle = IGrammarSymbol

[<Obsolete("Use IGrammarSymbol<'T> for individual grammar symbols or IGrammarBuilder<'T> for whole grammars instead.")>]
type internal DesigntimeFarkle<'TResult> = IGrammarSymbol<'TResult>

module DesigntimeFarkle =

    [<Obsolete("Use the WithOperatorScope extension method instead.")>]
    let inline withOperatorScope scope (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.WithOperatorScope scope

    [<Obsolete("Use the GrammarSymbol.rename or renameU functions instead.")>]
    let inline rename name (symbol: IGrammarSymbol<_>) =
        symbol.Rename name

    [<Obsolete("Use the CaseSensitive extension method instead.")>]
    let inline caseSensitive (flag: bool) (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.CaseSensitive flag

    [<Obsolete("Use the AutoWhitespace extension method instead.")>]
    let inline autoWhitespace (flag: bool) (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.AutoWhitespace flag

    [<Obsolete("Use the AddNoiseSymbol extension method instead.")>]
    let inline addNoiseSymbol name regex (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.AddNoiseSymbol(name, regex)

    [<Obsolete("Use the AddLineComment extension method instead.")>]
    let inline addLineComment commentStart (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.AddLineComment commentStart

    [<Obsolete("Use the AddBlockComment extension method instead.")>]
    let inline addBlockComment commentStart commentEnd (grammarBuilder: IGrammarBuilder<_>) =
        grammarBuilder.AddBlockComment(commentStart, commentEnd)

    [<Obsolete("Use the Cast extension method instead.")>]
    let inline cast (grammarBuilder: IGrammarBuilder) = grammarBuilder.Cast()
