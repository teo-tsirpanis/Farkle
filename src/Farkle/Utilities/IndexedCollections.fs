// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections
open System.Collections.Generic
open System.Collections.Immutable

/// A type-safe reference to a value based on its index.
type [<Struct>] Indexed<'a> = private Indexed of uint32
    with
        /// The index's numerical value.
        member x.Value = x |> (fun (Indexed x) -> x)

/// Functions for working with `Indexed<'a>`.
module internal Indexed =

    /// Creates an `Indexed` object, with the ability to explicitly specify its type.
    let create<'a> i: Indexed<'a> = Indexed i

#nowarn "0x06370000"

/// An immutable array that exhibits good random access performance and safe index access.
/// It intentionally lacks methods such as `map` and `filter`. This type should be at the final stage of data manipulation.
/// It is advised to work with sequences before, just until the end.
/// Safe random access is faciliated through `Indexed` objects.
type SafeArray<'a> = private SafeArray of 'a ImmutableArray
    with
        /// Creates a random-access list. Data will be copied to this new list.
        static member Create<'a> (x: 'a seq) = x.ToImmutableArray() |> SafeArray
        [<CompilerMessage("This method must be used only by the grammar domain model migrator, outside of this file.", 0x06370000)>]
        member private x.Value = x |> (fun (SafeArray x) -> x)
        /// Gets the length of the list.
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
                match x.Indexed i with
                | Some idx -> x.Item idx |> Some
                | None -> None
        /// Returns the index of the first element in the array that satisfies the given predicate, if there is any.
        member x.TryFindIndex f: Indexed<'a> option =
            x.Value |> Seq.tryFindIndex f |> Option.map (uint32 >> Indexed)
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
    /// Creates a `SafeArray` from an immutable array.
    let ofImmutableArray x = SafeArray x
    /// Creates a `SafeArray` from the given sequence.
    let inline ofSeq x = SafeArray.Create x
    /// Gets the item at the given position the `Indexed` object points to.
    /// Because it does not accept an arbitrary integer, it is less likely to accidentially fail.
    let inline retrieve (x: SafeArray<_>) i = x.Item i
    /// Returns an `Indexed` object that points to the position at the given index, or fails if it is out of bounds.
    let inline getIndex (x: SafeArray<_>) i = x.Indexed i
    /// Returns an item from an integer index, or fails if it is out of bounds.
    let inline getUnsafe (x: SafeArray<_>) i = x.ItemUnsafe i
    /// Returns the index of the first element in the array that satisfies the given predicate, if there is any.
    let inline tryFindIndex (x: SafeArray<_>) f = x.TryFindIndex f

/// A `SafeArray` of some "states", along with an initial one.
type StateTable<'a> =
    {
        /// The initial state. It should also be kept in the states as well.
        InitialState: 'a
        /// All the state table's states.
        States: SafeArray<'a>
    }
    with
        /// Gets the length of the state table.
        member x.Length = x.States.Count
        interface IEnumerable with
            /// [omit]
            member x.GetEnumerator() = (x.States :> IEnumerable).GetEnumerator()
        interface IEnumerable<'a> with
            /// [omit]
            member x.GetEnumerator() = (x.States :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<'a> with
            /// [omit]
            member x.Count = x.Length
