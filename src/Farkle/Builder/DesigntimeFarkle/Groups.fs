// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common

/// The base interface of groups.
type internal IGroup =
    inherit DesigntimeFarkle
    /// The sequence of characters that
    /// specifies the beginning of the group.
    abstract GroupStart: string
    /// The transformer to process the characters of this group.
    abstract Transformer: TransformerData

/// The typed implementation of the `IGroup` interface.
[<AbstractClass>]
type internal Group<'T>(name, groupStart, fTransform: T<'T>) =
    do nullCheck "name" name
    do nullCheck "groupStart" groupStart
    do nullCheck "fTransform" fTransform
    let tData = TransformerData.Create fTransform
    interface DesigntimeFarkle with
        member _.Name = name
    interface IGroup with
        member _.GroupStart = groupStart
        member _.Transformer = tData
    interface DesigntimeFarkle<'T>
    interface IExposedAsDesigntimeFarkleChild with
        member x.WithMetadataSameType name metadata =
            DesigntimeFarkleWrapper<'T>(name, metadata, x) :> _

/// The base, untyped interface of line groups.
/// A line group starts with a literal and ends when the line changes.
type internal ILineGroup =
    inherit IGroup

/// The typed implementation of the `ILineGroup` interface.
[<Sealed; NoComparison>]
type internal LineGroup<'T>(name, groupStart, transformer) =
    inherit Group<'T>(name, groupStart, transformer)
    interface ILineGroup

/// The base, untyped interface of block groups.
/// A block group starts and ends with a literal.
type internal IBlockGroup =
    inherit IGroup
    /// The sequence of characters that specifies the end of the group.
    abstract GroupEnd: string

/// The typed implementation of the `IBlockGroup` interface.
[<Sealed; NoComparison>]
type internal BlockGroup<'T>(name, groupStart, groupEnd, transformer) =
    inherit Group<'T>(name, groupStart, transformer)
    do nullCheck "groupEnd" groupEnd
    interface IBlockGroup with
        member _.GroupEnd = groupEnd
