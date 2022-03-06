// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
open System
open System.Runtime.CompilerServices

/// A helper static class to create terminals.
[<AbstractClass; Sealed>]
type Terminal =
    /// <summary>Creates a terminal that contains significant
    /// information of type <typeparamref name="T"/>.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="fTransform">The function that transforms
    /// the terminal's position and data to <typeparamref name="T"/>.
    /// Must not be null.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    static member Create<[<Nullable(0uy)>] 'T>(name, fTransform: T<'T>, regex) =
        nullCheck "name" name
        nullCheck "fTransform" fTransform
        Terminal(name, regex, fTransform) :> DesigntimeFarkle<_>
    /// <summary>Creates a terminal that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    static member Create(name, regex) =
        nullCheck "name" name
        {new ITerminal with
            member _.Name = name
            member _.Regex = regex
            member _.Transformer = TransformerData.Null
            // This type does not have a transformer and won't
            // inhibit unloadability in the precompiler.
            member x.IdentityObject = x} :> DesigntimeFarkle
    /// <summary>Creates a virtual terminal. This method is
    /// intended for use in advanced scenarios.</summary>
    /// <param name="name">The virtual terminal's name</param>
    /// <seealso cref="Farkle.Builder.VirtualTerminal"/>
    static member Virtual name =
        VirtualTerminal name :> DesigntimeFarkle
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
    /// <c>\r</c> (deprecated and will be removed in a future release),
    /// <c>\n</c>, or <c>\r\n</c>.</remarks>
    static member NewLine = NewLine :> DesigntimeFarkle

/// A helper static class to create nonterminals.
[<AbstractClass; Sealed>]
type Nonterminal =
    /// <summary>Creates a <see cref="Nonterminal{T}"/> whose productions must be
    /// later set with <see cref="SetProductions"/>. Useful for recursive productions.</summary>
    /// <remarks>If the productions are not set, an error will be raised on building.</remarks>
    static member Create<[<Nullable(0uy)>] 'T>(name) =
        NonterminalReal<'T> name :> Nonterminal<'T>

    /// <summary>Creates a <see cref="DesigntimeFarkle{T}"/> that represents
    /// a nonterminal with a given name and productions.</summary>
    static member Create<[<Nullable(0uy)>] 'T>(name, firstProduction, [<ParamArray>] productions) =
        let nont = Nonterminal.Create name
        nont.SetProductions(firstProduction, productions)
        nont :> DesigntimeFarkle<'T>

    /// <inheritdoc cref="Farkle.Builder.Untyped.Nonterminal.Create" />
    static member CreateUntyped(name) = Untyped.Nonterminal.Create name

    /// <inheritdoc cref="Farkle.Builder.Untyped.Nonterminal.Create" />
    static member CreateUntyped(name, firstProd, prods) =
        Untyped.Nonterminal.Create(name, firstProd, prods)

/// A helper static class to create groups.
/// Groups are used to define lexical elements that start and
/// end with specified literals, and contain arbitrary characters.
/// Groups are a tokenizer's construct, and their content is
/// considered to be a terminal by the parser.
/// Comments are essentially groups, but this class
/// creates groups that have significant content.
[<AbstractClass; Sealed>]
type Group =
    /// <summary>Creates a line group. As the name says, it ends with a new line.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="fTransform">The function that transforms
    /// the group's position and data to <typeparamref name="T"/>. Must not be null.
    /// The given position is the position where <paramref name="groupStart"/> starts
    /// and the group's data do not include the new line that end it.</param>
    static member Line<[<Nullable(0uy)>] 'T>(name, groupStart, fTransform: T<'T>) =
        LineGroup(name, groupStart, fTransform) :> DesigntimeFarkle<_>
    /// <summary>Creates a block group. Block groups end with a string literal.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="groupEnd"> The sequence of characters
    /// that specify the end of the group.</param>
    /// <param name="fTransform">The function that transforms
    /// the group's position and data to <typeparamref name="T"/>. Must not be null.
    /// The given position is the position where <paramref name="groupStart"/> starts
    /// and the group's data do include <paramref name="groupEnd"/>.</param>
    static member Block<[<Nullable(0uy)>] 'T>(name, groupStart, groupEnd, fTransform: T<'T>) =
        BlockGroup(name, groupStart, groupEnd, fTransform) :> DesigntimeFarkle<_>
    /// <summary>Creates a line group that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    static member Line(name, groupStart) =
        nullCheck "name" name
        nullCheck "groupStart" groupStart
        {new ILineGroup with
            member _.Name = name
            member _.GroupStart = groupStart
            member _.Transformer = TransformerData.Null} :> DesigntimeFarkle
    /// <summary>Creates a line group that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="groupEnd"> The sequence of characters
    /// that specify the end of the group.</param>
    static member Block(name, groupStart, groupEnd) =
        nullCheck "name" name
        nullCheck "groupStart" groupStart
        nullCheck "groupEnd" groupEnd
        {new IBlockGroup with
            member _.Name = name
            member _.GroupStart = groupStart
            member _.GroupEnd = groupEnd
            member _.Transformer = TransformerData.Null} :> DesigntimeFarkle
