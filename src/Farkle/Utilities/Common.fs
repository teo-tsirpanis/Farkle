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
type Indexable<'a> =
    {
        Index: uint16
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
type Indexed<'a> = Indexed of uint16

/// Functions for working with `Indexed<'a>`.
module Indexed =
    /// Converts an `Indexed` value to an actual object based on an index-retrieving function.
    /// In case the index is not found, the function fails.
    let get f (i: Indexed<'a>) : Result<'a, uint16> =
        let (Indexed i) = i
        match f i with
        | Some x -> ok x
        | None -> fail i

    /// Converts an `Indexed` value to an actual object lased on the index in a specified list.
    let getfromList list i =
        let fget i = i |> int |> List.tryItem <| list
        get fget i

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

/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
type Position = private Position of (uint32 * uint32)

/// Functions to work with the `Position` type.
module Position =

    open LanguagePrimitives

    /// Returns the line of a `Position.
    let line (Position(x, _)) = x

    /// Returns the column of a `Position`.
    let column (Position(_, x)) = x

    /// Returns a `Position` that points at `(1, 1)`.
    let initial = (GenericOne, GenericOne) |> Position

    /// Creates a `Position` at the specified coordinates.
    /// Returns `None` if a coordinate was zero.
    let create line col =
        if line <= GenericZero || col <= GenericZero then
            None
        else
            (line, col) |> Position |> Some
    
    /// Increases the column index of a `Position` by one.
    let incCol (Position (x, y)) = (x, y + GenericOne) |> Position

    /// Increades the line index of a `Position` by one and resets the collumn to one.
    let incLine (Position(x, _)) = (x + GenericOne, GenericOne) |> Position

/// A stack implemented using the ubiquitous F# list.
type Stack<'a> = Stack of 'a list

/// Functions for working with `Stack`s.
module Stack =

    /// An empty stack.
    let empty = Stack []

    /// Pushes an item to the front of a `Stack`.
    let inline push (Stack xs) x = x :: xs |> Stack

    /// Returns a new `Stack` with its first item removed.
    /// If the stack is empty, nothing happens.
    let inline pop (Stack x) =
        match x with
        | x :: xs -> Stack xs
        | [] -> empty

    /// Returns the first item of a `Stack`.
    /// If there are no items, `None` is returned.
    let inline tryPeek (Stack x) =
        match x with
        | x :: _ -> Some x
        | [] -> None

    /// A lens for the `Stack`'s underlying list.
    let Stack_ = (fun (Stack x) -> x), (fun x (Stack _) -> x |> Stack)