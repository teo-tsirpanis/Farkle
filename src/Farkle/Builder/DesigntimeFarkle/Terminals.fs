// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
open Farkle.Grammars
open System

/// <summary>The base, untyped interface of <see cref="Terminal{T}"/>.</summary>
/// <seealso cref="Terminal{T}"/>
type internal AbstractTerminal =
    inherit DesigntimeFarkle
    /// <summary>The <see cref="Regex"/> that defines this terminal.</summary>
    abstract Regex: Regex
    /// The transformer to process the characters of this terminal.
    abstract Transformer: TransformerData

/// <summary>A terminal symbol.</summary>
/// <typeparam name="T">The type of the objects this terminal generates.</typeparam>
type internal Terminal<'T>(name, regex, fTransform: T<'T>) =
    do nullCheck (nameof name) name
    do nullCheck (nameof fTransform) fTransform
    let tData = TransformerData.Create fTransform
    interface AbstractTerminal with
        member _.Regex = regex
        member _.Transformer = tData
    interface DesigntimeFarkle with
        member _.Name = name
    interface DesigntimeFarkle<'T>
    interface IExposedAsDesigntimeFarkleChild with
        member x.WithMetadataSameType name metadata =
            DesigntimeFarkleWrapper<'T>(name, metadata, x) :> _

/// <summary>A terminal that is not backed by the tokenizer.</summary>
/// <remarks><para>Virtual terminals don't have a regex associated with them
/// and the tokenizer will never extract a virtual terminal from source text.
/// Instead, they have to be created by the developer from a custom descendant
/// of the <see cref="Farkle.Parser.Tokenizer"/> class.</para>
/// <para>They are useful for indentation-based
/// languages like Python and F#.</para></remarks>
type internal VirtualTerminal internal(name) =
    do nullCheck "name" name
    /// The virtual terminal's name.
    member _.Name = name
    interface DesigntimeFarkle with
        member _.Name = name

type internal Literal(str: string) =
    do nullCheck "str" str
    member _.Content = str
    override _.Equals(x) =
        match x with
        | null -> false
        | :? Literal as lit -> str.Equals(lit.Content, StringComparison.Ordinal)
        | _ -> false
    override _.GetHashCode() = str.GetHashCode()
    interface DesigntimeFarkle with
        member _.Name =
            // This would make error messages clearer
            // when an empty literal string is created.
            if String.IsNullOrEmpty(str) then
                "Empty String"
            else
                str

/// <summary>A special kind of <see cref="DesigntimeFarkle"/>
/// that represents a new line.</summary>
type internal NewLine = NewLine
with
    interface DesigntimeFarkle with
        member _.Name = Terminal.NewLineName
