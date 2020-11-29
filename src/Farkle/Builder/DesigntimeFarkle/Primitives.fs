// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open System.Collections.Immutable

[<NoComparison>]
/// <summary>The base interface of <see cref="DesigntimeFarkle{T}"/>.</summary>
/// <remarks><para>In contrast with its typed descendant, untyped designtime
/// Farkles do not return any value. They typically represent literal symbols
/// that can only take one value. Building an untyped designtime Farkle will
/// result in a syntax-checking runtime Farkle with no custom post-processor.</para>
/// <para>User code must not implement this interface,
/// or an exception might be thrown.</para></remarks>
/// <seealso cref="DesigntimeFarkle{T}"/>
type DesigntimeFarkle =
    /// The name of the starting symbol.
    abstract Name: string
    /// <summary>The associated <see cref="GrammarMetadata"/> object.</summary>
    abstract Metadata: GrammarMetadata

/// <summary>An object representing a grammar symbol created by Farkle.Builder.
/// It is usually either a terminal or a nonterminal, containing other
/// designtime Farkles.</summary>
/// <remarks><para>Designtime Farkles cannot be used to parse text but can be
/// composed into larger designtime Farkles. To actually use them, they
/// have to be converted to a <see cref="RuntimeFarkle{T}"/> which however
/// is not composable. This conversion is performed by the <c>RuntimeFarkle.Build</c>
/// function or the <c>Build</c> extension method.</para>
/// <para>This interface has no members on its own; they are
/// inherited from <see cref="DesigntimeFarkle"/></para>
/// <para>User code must not implement this interface,
/// or an exception might be thrown.</para></remarks>
/// <typeparam name="T">The type of the objects this grammar generates.</typeparam>
/// <seealso cref="DesigntimeFarkle"/>
type DesigntimeFarkle< [<CovariantOut>] 'T> =
    inherit DesigntimeFarkle

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
