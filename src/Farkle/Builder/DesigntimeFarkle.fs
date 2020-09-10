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
/// <summary>A delegate that transforms the content
/// of a terminal to an arbitrary object.</summary>
/// <param name="context">An <see cref="ITransformerContext"/>
/// that provides additional info about the terminal.</param>
/// <param name="data">A read-only span of the terminal's characters.</param>
/// <remarks>
/// <para>In F# this type is shortened to
/// <c>T</c> to avoid clutter in user code.</para>
/// <para>A .NET delegate was used because read-only
/// spans are incompatible with F# functions.</para>
/// </remarks>
type T<[<CovariantOut>] 'T> = delegate of context: ITransformerContext * data: ReadOnlySpan<char> -> 'T

[<CompiledName("Fuser`1")>]
/// <summary>A delegate that fuses the many members
/// of a production into one arbitrary object.</summary>
/// <param name="members">A read-only span of the production's members.</param>
/// <remarks>
/// <para>In F# this type is shortened to
/// <c>F</c> to avoid clutter in user code.</para>
/// <para>A .NET delegate was used because read-only
/// spans are incompatible with F# functions.</para>
/// </remarks>
type F<[<CovariantOut>] 'T> = delegate of members: ReadOnlySpan<obj> -> 'T

module internal T =
    /// Converts a `T` callback so that it returns an object.
    let box (f: T<'T>) =
        // https://stackoverflow.com/questions/12454794
        if typeof<'T>.IsValueType then
            T(fun context data -> f.Invoke(context, data) |> box)
        else
            unbox f

module internal F =
    /// Converts an `F` delegate so that it returns an object.
    let box (f: F<'T>) =
        if typeof<'T>.IsValueType then
            F(fun data -> f.Invoke(data) |> box)
        else
            unbox f

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
    /// A stricter set of metadata for a grammar.
    /// They specify a case sensitive grammar without any whitespace allowed.
    static member Strict = {
        GrammarMetadata.Default with
            CaseSensitive = true
            AutoWhitespace = false
    }

[<NoComparison>]
/// <summary>The base untyped interface of <see cref="DesigntimeFarkle{T}"/>.</summary>
/// <remarks>User code must not implement this interface,
/// or an exception might be thrown.</remarks>
/// <seealso cref="DesigntimeFarkle{T}"/>
type DesigntimeFarkle =
    /// The name of the starting symbol.
    abstract Name: string
    /// <summary>The associated <see cref="GrammarMetadata"/> object.</summary>
    abstract Metadata: GrammarMetadata

/// <summary>An object representing a grammar created by Farkle.Builder.
/// It can be converted to a <see cref="RuntimeFarkle{T}"/>.</summary>
/// <remarks>User types must not implement this interface,
/// or an exception might be thrown.</remarks>
/// <typeparam name="T">The type of the objects this grammar generates.</typeparam>
/// <seealso cref="DesigntimeFarkle"/>
type DesigntimeFarkle< [<CovariantOut>] 'T> =
    inherit DesigntimeFarkle

/// <summary>The base, untyped interface of <see cref="Terminal{T}"/>.</summary>
/// <seealso cref="Terminal{T}"/>
type internal AbstractTerminal =
    inherit DesigntimeFarkle
    /// <summary>The <see cref="Regex"/> that defines this terminal.</summary>
    abstract Regex: Regex
    /// The delegate that converts the terminal's position and data into an arbitrary object.
    abstract Transformer: T<obj>

type internal Literal = Literal of string
with
    interface DesigntimeFarkle with
        member x.Name =
            match x with
            // This would make error messages clearer
            // when an empty literal string is created.
            | Literal x when String.IsNullOrEmpty(x) -> "Empty String"
            | Literal x -> x
        member __.Metadata = GrammarMetadata.Default

/// <summary>A special kind of <see cref="DesigntimeFarkle"/>
/// that represents a new line.</summary>
type internal NewLine = NewLine
with
    interface DesigntimeFarkle with
        member __.Name = "NewLine"
        member __.Metadata = GrammarMetadata.Default

/// The base interface of groups.
type internal AbstractGroup =
    inherit DesigntimeFarkle
    /// The sequence of characters that
    /// specifies the beginning of the group.
    abstract GroupStart: string
    /// The delegate that converts the group's position and data into an arbitrary object.
    abstract Transformer: T<obj>

/// The base, untyped interface of line groups.
/// A line group starts with a literal and ends when the line changes.
type internal AbstractLineGroup =
    inherit AbstractGroup

/// The base, untyped interface of block groups.
/// A block group starts and ends with a literal.
type internal AbstractBlockGroup =
    inherit AbstractGroup
    /// The sequence of characters that specifies the end of the group.
    abstract GroupEnd: string

/// <summary>The base, untyped interface of <see cref="Production{T}"/>.</summary>
/// <seealso cref="Production{T}"/>
// This type's naming differs from the other interfaces, because there is
// an module that must be called `Production` (so that it has a C#-friendly name).
type internal AbstractProduction =
    /// The members of the production.
    abstract Members: DesigntimeFarkle ImmutableArray
    abstract Fuse: F<obj>

/// <summary>The base, untyped interface of <see cref="Nonterminal{T}"/>.</summary>
/// <seealso cref="Nonterminal{T}"/>
type internal AbstractNonterminal =
    inherit DesigntimeFarkle
    /// Makes the nonterminal's productions immutable.
    /// This function was introduced to add more determinism
    /// to the limited mutability allowed in nonterminals.
    abstract Freeze: unit -> unit
    /// The productions of the nonterminal.
    abstract Productions: AbstractProduction list

type internal DesigntimeFarkleWrapper =
    abstract InnerDesigntimeFarkle: DesigntimeFarkle
    inherit DesigntimeFarkle

[<NoComparison; ReferenceEquality>]
type internal DesigntimeFarkleWrapper<'T> = {
    InnerDesigntimeFarkle: DesigntimeFarkle
    Name: string
    Metadata: GrammarMetadata
}
with
    static member Create (df: DesigntimeFarkle<'T>) =
        match df with
        | :? DesigntimeFarkleWrapper<'T> as dfw -> dfw
        | _ -> {InnerDesigntimeFarkle = df; Name = df.Name; Metadata = GrammarMetadata.Default}
    interface DesigntimeFarkle with
        member x.Name = x.Name
        member x.Metadata = x.Metadata
    interface DesigntimeFarkleWrapper with
        member x.InnerDesigntimeFarkle = x.InnerDesigntimeFarkle
    interface DesigntimeFarkle<'T>

[<NoComparison; ReferenceEquality>]
/// <summary>A terminal symbol.</summary>
/// <typeparam name="T">The type of the objects this terminal generates.</typeparam>
type internal Terminal<'T> = {
    _Name: string
    Regex: Regex
    Transformer: T<obj>
}
with
    interface AbstractTerminal with
        member x.Regex = x.Regex
        member x.Transformer = x.Transformer
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>

[<AbstractClass; Sealed>]
/// A helper static class to create terminals.
// Can't move this one to the operators, because the untyped API uses it.
type Terminal =
    /// <summary>Creates a terminal that contains significant
    /// information of type <typeparamref name="T"/>.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="fTransform">The function that transforms
    /// the terminal's position and data to <typeparamref name="T"/>.
    /// Must not be null.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    static member Create (name, fTransform: T<'T>, regex) =
        nullCheck "name" name
        nullCheck "fTransform" fTransform
        let term = {
            _Name = name
            Regex = regex
            Transformer = T.box fTransform
        }
        term :> DesigntimeFarkle<'T>
    /// <summary>Creates a terminal that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    static member Create(name, regex) =
        nullCheck "name" name
        {new AbstractTerminal with
            member __.Name = name
            member __.Metadata = GrammarMetadata.Default
            member __.Regex = regex
            member __.Transformer = null} :> DesigntimeFarkle
    /// <summary>Creates a terminal that recognizes a literal string.</summary>
    /// <param name="str">The string literal this terminal will recognize.</param>
    /// <remarks>It does not return anything.</remarks>
    static member Literal(str) =
        nullCheck "str" str
        Literal str :> DesigntimeFarkle
    /// <summary>A special kind of <see cref="DesigntimeFarkle"/>
    /// that represents a new line.</summary>
    /// <remarks>This is different and better than a literal of
    /// newline characters. If used anywhere in a grammar, it indicates that it
    /// is line-based, which means that newline characters are not noise.
    /// Newline characters are considered the character sequences
    /// <c>\r</c>, <c>\n</c>, or <c>\r\n</c>.</remarks>
    static member NewLine = NewLine :> DesigntimeFarkle

/// <summary>A production. Productions are parts of <see cref="Nonterminal{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this production generates.</typeparam>
type [<NoComparison; ReferenceEquality>] Production<'T> = internal {
    Members: DesigntimeFarkle ImmutableArray
    Fuse: F<obj>
}
with
    interface AbstractProduction with
        member x.Members = x.Members
        member x.Fuse = x.Fuse

[<NoComparison; ReferenceEquality>]
/// <summary>A nonterminal symbol. It is made of <see cref="Production{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this nonterminal generates.
/// All productions of a nonterminal have the same type parameter.</typeparam>
type Nonterminal<'T> = internal {
    _Name: string
    Productions: SetOnce<AbstractProduction list>
}
with
    /// The nonterminal's name.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must only be called once, and before
    /// building a designtime Farkle containing this one.
    /// Subsequent calls (and these after building) are ignored.</remarks>
    member x.SetProductions(firstProd: Production<'T>, [<ParamArray>] prods: Production<'T> []) =
        prods
        |> Seq.map (fun x -> x :> AbstractProduction)
        |> List.ofSeq
        |> (fun prods -> (firstProd :> AbstractProduction) :: prods)
        |> x.Productions.TrySet
        |> ignore
    interface AbstractNonterminal with
        // If they are already set, nothing will happen.
        // If they haven't been set, they will be permanently
        // set to a broken state.
        member x.Freeze() = x.Productions.TrySet [] |> ignore
        member x.Productions = x.Productions.ValueOrDefault []
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>

[<AbstractClass>]
/// The typed implementation of the `AbstractGroup` interface.
type internal Group<'T>(name, groupStart, transformer: T<'T>) =
    do nullCheck "name" name
    do nullCheck "groupStart" groupStart
    do nullCheck "transformer" transformer
    let transformerBoxed = T.box transformer
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = GrammarMetadata.Default
    interface AbstractGroup with
        member _.GroupStart = groupStart
        member _.Transformer = transformerBoxed
    interface DesigntimeFarkle<'T>

[<Sealed; NoComparison>]
/// The typed implementation of the `AbstractLineGroup` interface.
type internal LineGroup<'T>(name, groupStart, transformer) =
    inherit Group<'T>(name, groupStart, transformer)
    interface AbstractLineGroup

[<Sealed; NoComparison>]
/// The typed implementation of the `AbstractBlockGroup` interface.
type internal BlockGroup<'T>(name, groupStart, groupEnd, transformer) =
    inherit Group<'T>(name, groupStart, transformer)
    do nullCheck "groupEnd" groupEnd
    interface AbstractBlockGroup with
        member _.GroupEnd = groupEnd
