// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections
open System.Collections.Generic

/// A type-safe reference to a value based on its index.
type [<Struct>] Indexed<'a> = private Indexed of uint32
    with
        /// The index's numerical value.
        member x.Value = x |> (fun (Indexed x) -> x)
        /// Changes the type of the indexed object.
        [<CompilerMessage("This function must be used only for the grammar domain model migrator.", 0x06400000)>]
        member internal x.ReInterpret<'b>(): Indexed<'b> = Indexed x.Value

/// Functions for working with `Indexed<'a>`.
module internal Indexed =

    /// Creates an `Indexed` object, with the ability to explicitly specify its type.
    let create<'a> i: Indexed<'a> = Indexed i

    let inline createWithKnownLength<'a> (arr: IReadOnlyCollection<'a>) (i: uint16) =
        if int i <= arr.Count then
            i |> uint32 |> create<'a> |> Some
        else
            None

#nowarn "0x06370000"

/// An immutable array that exhibits good random access performance and safe index access.
/// It intentionally lacks methods such as `map` and `filter`. This type should be at the final stage of data manipulation.
/// It is advised to work with sequences before, just until the end.
/// Safe random access is faciliated through `Indexed` objects.
[<Struct>]
type SafeArray<'a> = private SafeArray of 'a[]
    with
        /// O(n) Creates a random-access list. Data will be copied to this new list.
        static member Create<'a> (x: 'a seq) =
            x
            |> Array.ofSeq
            |> SafeArray
        [<CompilerMessage("This method must be used only by the grammar domain model migrator, outside of this file.", 0x06370000)>]
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
                match x.Indexed i with
                | Some idx -> x.Item idx |> Some
                | None -> None
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
    /// Creates a `SafeArray` directly from an array without copying its data.
    /// However, to maintain immutability, the user has to make sure that this array is nowhere else referenced.
    let internal ofArrayUnsafe x = SafeArray x
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

type StateTable<'a> =
    {
        InitialState: 'a
        States: SafeArray<'a>
    }
    with
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
