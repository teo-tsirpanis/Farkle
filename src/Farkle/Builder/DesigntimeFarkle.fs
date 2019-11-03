// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Common
open Farkle.Collections
open System
open System.Collections.Immutable

[<CompiledName("TerminalCallback`1")>]
/// <summary>A delegate that accepts the position and data of a terminal, and transforms them into an arbitrary object.</summary>
/// <remarks>
///     <para>In F#, this type is named <c>T</c> - from "Terminal" and was shortened to avoid clutter in user code.</para>
///     <para>A .NET delegate was used because <see cref="ReadOnlySpan{Char}"/>s are incompatible with F# functions</para>
/// </remarks>
type T<'T> = delegate of Position * ReadOnlySpan<char> -> 'T

/// A type of source code comment. As everybody might know,
/// comments are the text fragments that are ignored by the parser.
type Comment =
    /// A line comment. It starts when the given literal is encountered,
    /// and ends when the line ends.
    | LineComment of string
    /// A block comment. It starts when the first literal is encountered,
    /// and ends when when the second literal is encountered.
    | BlockComment of blockStart: string * blockEnd: string

/// The information about a grammar that cannot be expressed
/// by its terminals and nonterminals.
type GrammarMetadata = {
    /// Whether the grammar is case sensitive.
    CaseSensitive: bool
    /// Whether to discard any whitespace characters encountered
    /// outside of any terminal. Farkle considers whitespace the
    /// characters: Space, Horizontal Tab, Carriage Return and Line feed.
    AutoWhitespace: bool
    /// The comments this grammar accepts.
    Comments: Comment ImmutableList
    /// Any other symbols definable by a regular expression that can
    /// appear anywhere outside of any terminal and will be discarded.
    NoiseSymbols: (string * Regex) ImmutableList
}
with
    /// The default metadata of a grammar.
    /// According to them, the grammar is not case sensitive
    /// and white space is discarded.
    static member Default = {
        CaseSensitive = false
        AutoWhitespace = true
        NoiseSymbols = ImmutableList.Empty
        Comments = ImmutableList.Empty
    }

/// <summary>The base, untyped interface of <see cref="DesigntimeFarkle{T}"/>.</summary>
/// <remarks>User code must not implement this interface, or an error may be raised.</remarks>
/// <seealso cref="DesigntimeFarkle{T}"/>
type DesigntimeFarkle =
    /// The name of the starting symbol.
    abstract Name: string
    /// <summary>The associated <see cref="GrammarMetadata"/> object.</summary>
    abstract Metadata: GrammarMetadata

/// <summary>The base, untyped interface of <see cref="Terminal{T}"/>.</summary>
/// <seealso cref="Terminal{T}"/>
type internal AbstractTerminal =
    inherit DesigntimeFarkle
    /// <summary>The <see cref="Regex"/> that defines this terminal.</summary>
    abstract Regex: Regex
    /// The delagate that converts the terminal's position and data into an arbitrary object.
    abstract Transformer: T<obj>

type internal Literal = Literal of string
with
    interface DesigntimeFarkle with
        member x.Name =
            match x with
            // This would make things clearer when an empty literal string is created.
            | Literal x when String.IsNullOrEmpty(x) -> "Empty String"
            | Literal x -> x
        member __.Metadata = GrammarMetadata.Default

/// <summary>The base, untyped interface of <see cref="Nonterminal{T}"/>.</summary>
/// <seealso cref="Nonterminal{T}"/>
type internal AbstractNonterminal =
    inherit DesigntimeFarkle
    /// The productions of the nonterminal.
    abstract Productions: AbstractProduction list

/// <summary>The base, untyped interface of <see cref="Production{T}"/>.</summary>
/// <seealso cref="Production{T}"/>
// This type's naming differs from the other interfaces, because there is
// an module that must be called `Production` (so that it has a C#-friendly name).
and internal AbstractProduction =
    /// The members of the production.
    abstract Members: Symbol ImmutableArray
    abstract Fuse: (obj [] -> obj)

and internal Symbol = Choice<AbstractTerminal, Literal, AbstractNonterminal>

/// <summary>An object representing a grammar created by Farkle.Builder.
/// It can be converted to <see cref="RuntimeFarkle{T}"/>.</summary>
/// <remarks>
///     <para>This interface is implemented by <see cref="Terminal{T}"/> and <see cref="Nonterminal{T}"/>.</para>
///     <para>User code must not implement this interface, or an error may be raised.</para>
/// </remarks>
/// <typeparam name="T">The type of the objects this grammar generates.</typeparam>
type DesigntimeFarkle<'T> = 
    inherit DesigntimeFarkle

type internal DesigntimeFarkleWithMetadata =
    abstract InnerDesigntimeFarkle: DesigntimeFarkle

type internal DesigntimeFarkleWithMetadata<'T> = {
    InnerDesigntimeFarkle: DesigntimeFarkle<'T>
    Metadata: GrammarMetadata
}
with
    static member Create (df: DesigntimeFarkle<'T>) =
        match df with
        | :? DesigntimeFarkleWithMetadata<'T> as dfwm -> dfwm
        | _ -> {InnerDesigntimeFarkle = df; Metadata = GrammarMetadata.Default}
    interface DesigntimeFarkle<'T> with
        member x.Name = x.InnerDesigntimeFarkle.Name
        member x.Metadata = x.Metadata
    interface DesigntimeFarkleWithMetadata with
        member x.InnerDesigntimeFarkle = upcast x.InnerDesigntimeFarkle

[<NoComparison; ReferenceEquality>]
/// <summary>A terminal symbol.</summary>
/// <typeparam name="T">The type of the objects this terminal generates.</typeparam>
type Terminal<'T> = internal {
    _Name: string
    Regex: Regex
    Transformer: T<obj> 
}
with
    /// <summary>Creates a <see cref="Terminal{T}"/>.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="fTransform">The function that transforms the terminal's position and data to <c>T</c>.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    static member Create name (fTransform: T<'T>) regex =
        let term = {
            _Name = name
            Regex = regex
            Transformer = T(fun pos data -> fTransform.Invoke(pos, data) |> box)
        }
        term :> DesigntimeFarkle<'T>
    /// The terminal's name.
    member x.Name = x._Name
    interface AbstractTerminal with
        member x.Regex = x.Regex
        member x.Transformer = x.Transformer
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>

[<NoComparison; ReferenceEquality>]
/// <summary>A nonterminal symbol. It is made of <see cref="Production{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this nonterminal generates. All productions of a nonterminal have the same type parameter.</typeparam>
type Nonterminal<'T> = internal {
    _Name: string
    Productions: SetOnce<AbstractProduction list>
}
with
    /// The nonterminal's name.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must only be called once. Subsequent calls are ignored.</remarks>
    member x.SetProductions([<ParamArray>] prods: Production<'T> []) =
        prods
        |> Seq.map (fun x -> x :> AbstractProduction)
        |> List.ofSeq
        |> x.Productions.TrySet
        |> ignore
    interface AbstractNonterminal with
        member x.Productions = x.Productions.ValueOrDefault []
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>

/// <summary>A production. Productions are parts of <see cref="Nonterminal{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this production generates.</typeparam>
and Production<'T> = internal {
    Members: Symbol ImmutableArray
    Fuse: obj [] -> obj
}
with
    interface AbstractProduction with
        member x.Members = x.Members
        member x.Fuse = x.Fuse

module internal Symbol =
    let rec specialize (x: DesigntimeFarkle): Symbol =
        match x with
        | :? AbstractTerminal as term -> Choice1Of3 term
        | :? Literal as lit -> Choice2Of3 lit
        | :? AbstractNonterminal as nont -> Choice3Of3 nont
        | :? DesigntimeFarkleWithMetadata as x -> specialize x.InnerDesigntimeFarkle
        | _ -> failwith "Using a custom derivative of the DesigntimeFarkle interface is not allowed."
    let append xs df = ImmutableList.add xs (specialize df)

/// <summary>Functions to manipulate `DesigntimeFarkle`s.</summary>
/// <remarks>Warning: Specifying any metadata on the grammar has to be done
/// only at the topmost <see cref="DesigntimeFarkle{T}"/>. Otherwise they
/// will be disregarded.</remarks>
module DesigntimeFarkle =

    /// Sets a custom `GrammarMetadata` object to a `DesigntimeFarkle<T>`.
    let withMetadata metadata df =
        {DesigntimeFarkleWithMetadata.Create df with Metadata = metadata} :> DesigntimeFarkle<_>

    /// Sets the `CaseSensitive` field of a `DesigntimeFarkle`'s metadata.
    let caseSensitive flag df = df |> withMetadata {df.Metadata with CaseSensitive = flag}
    
    /// Sets the `AutoWhitespace` field of a `DesigntimeFarkle`'s metadata.
    let autoWhitespace flag df = df |> withMetadata {df.Metadata with AutoWhitespace = flag}

    /// Adds a name-`Regex` pair of noise symbols to the given `DesigntimeFarkle`.
    let addNoiseSymbol name regex df = df |> withMetadata {df.Metadata with NoiseSymbols = df.Metadata.NoiseSymbols.Add(name, regex)}

    /// Adds a line comment to the given `DesigntimeFarkle`.
    let addLineComment commentStart df =
        df |> withMetadata {df.Metadata with Comments = df.Metadata.Comments.Add(LineComment commentStart)}

    /// Adds a block comment to the given `DesigntimeFarkle`.
    let addBlockComment commentStart commentEnd df =
        df |> withMetadata {df.Metadata with Comments = df.Metadata.Comments.Add(BlockComment(commentStart, commentEnd))}
