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

    /// Creates an `Indexed` object, with the ability to explicitly specify its type.
    let create<'a> i: Indexed<'a> = Indexed i

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

/// An immutable array that exhibits good random-access performance and safe index access.
/// It intentionally lacks methods such as `map` and `filter`. This type should be at the final stage of data manipulation.
/// It is advised to work with sequences before, just until the end.
/// Safe random access is faciliated through `Indexed` objects.
type RandomAccessList<'a> = private RandomAccessList of ImmutableArray<'a>
    with
        /// O(n) Creates a random-access list. Data will be copied to this new list.
        static member Create x = x |> ImmutableArray.CreateRange |> RandomAccessList
        member private x.Value = x |> (fun (RandomAccessList x) -> x)
        /// O(1) Gets the length of the list.
        member x.Count = x.Value.Length
        /// Gets the item at the given position the `Indexed` object points to.
        /// Because it does not accept an arbitrary integer, it is less likely to accidentially fail.
        member x.Item
            with get (i: Indexed<'a>) = x.Value.[int i.Value]

        /// Returns an `Indexed` object that points to the position at the given index, or fails if it is out of bounds.
        member x.Indexed
            with get (i: uint32) =
                match i with
                | i when i < uint32 x.Count -> i |> Indexed.create<'a> |> Ok
                | i -> Error i
        interface IEnumerable with
            /// [omit]
            member x.GetEnumerator() = (x.Value :> IEnumerable).GetEnumerator()
        interface IEnumerable<'a> with
            /// [omit]
            member x.GetEnumerator() = (x.Value :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<'a> with
            /// [omit]
            member x.Count = x.Count

type [<Struct>] Keyed<'TKey,'TCorrespondingValue when 'TKey: comparison> = Keyed of 'TKey
    with
        member x.Key = x |> (fun (Keyed x) -> x)

module Keyed =
    let create<'TKey,'TCorrespondingValue when 'TKey: comparison> k: Keyed<'TKey,'TCorrespondingValue> = Keyed k

type RandomAccessMap<'TKey,'TValue when 'TKey: comparison> = private RandomAccessMap of Map<'TKey,'TValue>
    with
        static member Create x = x |> Map.ofSeq |> RandomAccessMap
        member private x.Value = x |> (fun (RandomAccessMap x) -> x)
        member x.ContainsKey k = x.Value.ContainsKey k
        member x.Count = x.Value.Count
        member x.Item
            with get (k: Keyed<'TKey, 'TValue>) = x.Value.[k.Key]

        member x.Keyed
            with get k =
                match k with
                | k when x.ContainsKey k -> k |> Keyed.create<'TKey,'TValue> |> Ok
                | k -> Error k
        interface IEnumerable with
            member x.GetEnumerator() = (x.Value :> IEnumerable).GetEnumerator()
        interface IEnumerable<KeyValuePair<'TKey,'TValue>> with
            member x.GetEnumerator() = (x.Value :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<KeyValuePair<'TKey,'TValue>> with
            member x.Count = x.Count
