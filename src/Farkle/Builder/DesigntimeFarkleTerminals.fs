// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

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
