// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open System.Runtime.CompilerServices

[<NoComparison>]
/// <summary>The base interface of <see cref="DesigntimeFarkle{T}"/>.</summary>
/// <remarks><para>In contrast with its typed descendant, untyped designtime
/// Farkles do not return any value. They typically represent literal symbols
/// that can only take one value. Building an untyped designtime Farkle will
/// result in a syntax-checking runtime Farkle with no custom post-processor.</para>
/// <para>User code must not implement this interface,
/// or an exception might be thrown.</para></remarks>
/// <seealso cref="DesigntimeFarkle{T}"/>
// A valid designtime farkle must either be/implement one of the fundamental
// designtime Farkle classes/interfaces, or implement DesigntimeFarkleWrapper.
// Because all these are internal, implementing a valid designtime Farkle outside
// this library is impossible.
type DesigntimeFarkle =
    /// <summary>The designtime Farkle's name.</summary>
    /// <remarks>A totally informative property, it matches
    /// the corresponding grammar symbol's name. Many designtime
    /// Farkles in a grammar can have the same name.</remarks>
    abstract Name: string

/// <summary>An object representing a grammar symbol created by Farkle.Builder.
/// It corresponds to either a standalone terminal or a nonterminal
/// that contains other designtime Farkles.</summary>
/// <remarks><para>Designtime Farkles cannot be used to parse text but can be
/// composed into larger designtime Farkles. To actually use them, they
/// have to be converted to a <see cref="RuntimeFarkle{T}"/> which however
/// is not composable. This one-way conversion is performed by the <c>RuntimeFarkle.build</c>
/// function or the <c>Build</c> extension method.</para>
/// <para>This interface has no members on its own; they are
/// inherited from <see cref="DesigntimeFarkle"/>.</para>
/// <para>User code must not implement this interface,
/// or an exception might be thrown.</para></remarks>
/// <typeparam name="T">The type of the objects this grammar generates.</typeparam>
/// <seealso cref="DesigntimeFarkle"/>
type DesigntimeFarkle< [<CovariantOut; Nullable(2uy)>] 'T> =
    inherit DesigntimeFarkle

/// This interface must be implemented by designtime Farkle types that
/// are publicly exposed as a descendant of the DesigntimeFarkle interface.
/// In other words these types include all typed designtime Farkles and typed and untyped nonterminals.
type internal IExposedAsDesigntimeFarkleChild =
    /// Creates a new designtime Farkle with the following name and metadata.
    /// The user-visible type of the returned designtime Farkle must not
    /// change, i.e. if this objectimplements a typed designtime Farkle, the
    /// resulting type must implement a typed designtime Farkle of the same type.
    abstract WithMetadataSameType: name: string -> metadata: GrammarMetadata -> DesigntimeFarkle

type internal DesigntimeFarkleWrapper =
    abstract InnerDesigntimeFarkle: DesigntimeFarkle
    abstract Metadata: GrammarMetadata
    inherit DesigntimeFarkle

type internal DesigntimeFarkleWrapper<'T>(name, metadata, inner) =
    static member Create (df: DesigntimeFarkle<'T>) =
        match df with
        | :? DesigntimeFarkleWrapper<'T> as dfw -> dfw
        | _ -> failwith ""
    interface DesigntimeFarkle with
        member _.Name = name
    interface DesigntimeFarkleWrapper with
        member _.InnerDesigntimeFarkle = inner
        member _.Metadata = metadata
    interface DesigntimeFarkle<'T>
    interface IExposedAsDesigntimeFarkleChild with
        member _.WithMetadataSameType name metadata =
            DesigntimeFarkleWrapper<'T>(name, metadata, inner) :> _
