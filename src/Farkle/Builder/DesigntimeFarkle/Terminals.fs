// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
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
    let tData = TransformerData.Create fTransform
    interface AbstractTerminal with
        member _.Regex = regex
        member _.Transformer = tData
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>

/// <summary>A terminal that is not backed by the tokenizer.</summary>
/// <remarks><para>Virtual terminals don't have a regex associated with them
/// and the tokenizer will never extract a virtual terminal from source text.
/// Instead, they have to be created by the developer from a custom descendant
/// of the <see cref="Farkle.Parser.Tokenizer"/> class.</para>
/// <para>They are useful for indentation-based languages like Python and F#.
/// TODO: add an example in documentation.</para></remarks>
type internal VirtualTerminal internal(name) =
    do nullCheck "name" name
    /// The virtual terminal's name.
    member _.Name = name
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = GrammarMetadata.Default

type internal Literal = Literal of string
with
    interface DesigntimeFarkle with
        member x.Name =
            match x with
            // This would make error messages clearer
            // when an empty literal string is created.
            | Literal x when String.IsNullOrEmpty(x) -> "Empty String"
            | Literal x -> x
        member _.Metadata = GrammarMetadata.Default

/// <summary>A special kind of <see cref="DesigntimeFarkle"/>
/// that represents a new line.</summary>
type internal NewLine = NewLine
with
    interface DesigntimeFarkle with
        member _.Name = "NewLine"
        member _.Metadata = GrammarMetadata.Default
