// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections.Generic
open System.Collections.Immutable
open System.Collections

/// Anything that can be indexed.
type Indexable =
    /// The object's index.
    abstract Index: uint32

module Indexable =
    /// Gets the index of an `Indexable` object.
    let index (x: #Indexable) = x.Index
    /// Sorts `Indexable` items based on their index.
    /// Duplicate indices do not raise an error.
    let collect x = x |> Seq.sortBy index |> ImmutableArray.CreateRange

/// A type-safe reference to a value based on its index.
type [<Struct>] Indexed<'a> = Indexed of uint32
    with
        member x.Value = x |> (fun (Indexed x) -> x)

/// Functions for working with `Indexed<'a>`.
module Indexed =

    open Operators.Checked

    /// Converts an `Indexed` value to an actual object lased on the index in a specified list.
    let getfromList (i: Indexed<'a>) (list: #IReadOnlyList<'a>) =
        let i = i |> (fun (Indexed i) -> i) |> int
        if list.Count > i then
            Ok list.[i]
        else
            Error <| uint32 i

/// An item and its index. A thin layer that makes items `Indexable` without cluttering their type definitions.
type IndexableWrapper<'a> =
    {
        /// The item.
        Item: 'a
        /// And the index.
        Index: uint32
    }
    interface Indexable with
        member x.Index = x.Index

/// Functions to work with `IndexableWrapper`s.
module IndexableWrapper =

    /// Creates an indexable wrapper
    let create index item = {Index = index; Item = item}

    /// Removes the indexable wrapper of an item.
    let item {Item = x} = x

    /// Sorts `Indexable` items based on their index and removes their wrapper.
    /// Duplicate indices do not raise an error.
    let collect x = x |> Seq.sortBy Indexable.index |> Seq.map item |> ImmutableArray.CreateRange
