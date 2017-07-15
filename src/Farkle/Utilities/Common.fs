// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
/// Some useful functions and types that could be used from many points from the library.
module Farkle.Common

open Chessie.ErrorHandling

/// Anything that can be indexed.
/// The type is just a record with a value and an index.
type Indexable<'a, 'TIndex> when 'TIndex: comparison =
    {
        Index: 'TIndex
        Item: 'a
    }

/// Functions for working with `Indexable<'a>`.
module Indexable =
    /// Gets the value of an `Indexable` object.
    let value {Item = x} = x
    /// Gets the index of an `Indexable` object.
    let index {Index = x} = x
    /// Creates an `Indexable` object.
    let create index x = {Index = index; Item = x}
    /// Sorts `Indexable` items based on their index, and removes it.
    /// Duplicate indices do not raise an error.
    let collect x = x |> List.ofSeq |> List.sortBy index |> List.map value

/// A type-safe reference to a value based on its index.
type Indexed<'a, 'TIndex> when 'TIndex: comparison = Indexed of 'TIndex

/// Functions for working with `Indexed<'a>`.
module Indexed =
    /// Converts an `Indexed` value to an actual object based on an index-retrieving function.
    /// In case the index is out of range, the function fails.
    let get f (i: Indexed<'a, 'b>) : Result<'a,'b> =
        let (Indexed i) = i
        match f i with
        | Some x -> ok x
        | None -> fail i

[<NoEquality; NoComparison>]
/// A simple and efficient set of items based on ranges.
/// Instead of storing _all_ the elements of a set, only the first and last are.
/// This closely follows the paradigm of GOLD Parser 5 sets.
type RangeSet<'a> when 'a: comparison = RangeSet of Set<('a * 'a)>

/// Functions to work with the `RangeSet` type.
module RangeSet =
    /// Creates a `RangeSet` that spans a single set from `x to `y`, _inclusive_.
    let create x y = (if x < y then (x, y) else (y, x)) |> Set.singleton |> RangeSet
    /// Creates a `RangeSet` that contains a single element.
    /// It should _not_ be exclusively used; this set is made for ranges.
    /// Use an F# set for sets with discrete items.
    let singleton x = create x x
    /// Combines two `RangeSet`s into a new one.
    let (++) (RangeSet x) (RangeSet y) = (x, y) ||> Set.union |> RangeSet
    /// Combines many `RangeSet`s into one.
    /// More efficient than `(++)`, if you work with more than two `RangeSet`s.
    let concat x = x |> Seq.map (fun (RangeSet x) -> x) |> Set.unionMany |> RangeSet
    /// Checks if a `RangeSet` contains an item.
    let contains (RangeSet s) x = s |> Set.exists (fun (a, b) -> x >= a && x <= b)