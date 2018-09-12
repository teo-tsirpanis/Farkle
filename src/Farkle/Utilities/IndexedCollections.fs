// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections
open System.Collections.Generic

/// Anything that can be indexed.
type Indexable =
    /// The object's index.
    abstract Index: uint32

module Indexable =
    /// Gets the index of an `Indexable` object.
    let index (x: #Indexable) = x.Index

/// A type-safe reference to a value based on its index.
type [<Struct>] Indexed<'a> = private Indexed of uint32
    with
        member x.Value = x |> (fun (Indexed x) -> x)

/// Functions for working with `Indexed<'a>`.
module Indexed =

    /// Creates an `Indexed` object, with the ability to explicitly specify its type.
    let create<'a> i: Indexed<'a> = Indexed i

    /// Creates an `Indexed` object that represents a `SafeArray` that will be created in the future, but has a known length.
    let internal createWithKnownLength16<'a> length (i: uint16) =
        if i <= length then
            i |> uint32 |> create<'a> |> Some
        else
            None

    let inline internal createWithKnownLength<'a> (arr: IReadOnlyCollection<'a>) (i: uint16) =
        if int i <= arr.Count then
            i |> uint32 |> create<'a> |> Some
        else
            None

/// An immutable array that exhibits good random access performance and safe index access.
/// It intentionally lacks methods such as `map` and `filter`. This type should be at the final stage of data manipulation.
/// It is advised to work with sequences before, just until the end.
/// Safe random access is faciliated through `Indexed` objects.
type SafeArray<'a> = private SafeArray of 'a[]
    with
        /// O(n) Creates a random-access list. Data will be copied to this new list.
        static member Create<'a> (x: 'a seq) =
            x
            |> Array.ofSeq
            |> SafeArray
        member private x.Value = x |> (fun (SafeArray x) -> x)
        /// O(1) Gets the length of the list.
        member x.Count = x.Value.Length
        /// Gets the item at the given position the `Indexed` object points to.
        /// Because it does not accept an arbitrary integer, it is less likely to accidentially fail.
        member x.Item
            with get (i: Indexed<'a>) = x.Value.[int i.Value]

        /// Returns an `Indexed` object that points to the position at the given index, or fails if it is out of bounds.
        member x.Indexed
            with get i: Indexed<'a> option =
                match i with
                | i when i < uint32 x.Count -> i |> Indexed |> Some
                | _ -> None
        /// Returns an item from an integer index, or fails if it is out of bounds.
        member x.ItemUnsafe
            with get i =
                x.Indexed i |> Option.map (fun i -> x.Item i)
        /// Returns the index of the first element in the array that satisfies the given predicate, if there is any.
        member x.TryFindIndex f: Indexed<'a> option =
            x.Value |> Array.tryFindIndex f |> Option.map (uint32 >> Indexed)
        interface IEnumerable with
            /// [omit]
            member x.GetEnumerator() = (x.Value :> IEnumerable).GetEnumerator()
        interface IEnumerable<'a> with
            /// [omit]
            member x.GetEnumerator() = (x.Value :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<'a> with
            /// [omit]
            member x.Count = x.Count

/// Functions to work with `SafeArray`s.
module SafeArray =
    /// Creates a `SafeArray` from the given sequence.
    let ofSeq x = SafeArray.Create x
    /// Creates a `SafeArray` from the given sequence of indexable objects that are sorted by their index.
    /// No special care is done for discontinuous or duplicate indices.
    let ofIndexables x = x |> Seq.sortBy Indexable.index |> ofSeq
    /// Gets the item at the given position the `Indexed` object points to.
    /// Because it does not accept an arbitrary integer, it is less likely to accidentially fail.
    let retrieve (x: SafeArray<_>) i = x.Item i
    /// Returns an `Indexed` object that points to the position at the given index, or fails if it is out of bounds.
    let getIndex (x: SafeArray<_>) i = x.Indexed i
    /// Returns an item from an integer index, or fails if it is out of bounds.
    let getUnsafe (x: SafeArray<_>) i = x.ItemUnsafe i
    /// Returns the index of the first element in the array that satisfies the given predicate, if there is any.
    let tryFindIndex (x: SafeArray<_>) f = x.TryFindIndex f

type StateTable<'a> =
    {
        InitialState: 'a
        States: SafeArray<'a>
    }
    with
        interface IEnumerable with
            /// [omit]
            member x.GetEnumerator() = (x.States :> IEnumerable).GetEnumerator()
        interface IEnumerable<'a> with
            /// [omit]
            member x.GetEnumerator() = (x.States :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<'a> with
            /// [omit]
            member x.Count = x.States.Count
