// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle


/// A simple and efficient set of items based on ranges.
/// Instead of storing _all_ the elements of a set, only the first and last are.
/// This closely follows the paradigm of GOLD Parser 5 sets.
type RangeSet<'a> when 'a: comparison = RangeSet of Set<('a * 'a)>

/// Functions to work with the `RangeSet` type.
module RangeSet =

    /// Creates a `RangeSet` that spans a single set from `x to `y`, _inclusive_.
    let create x y = (if x < y then (x, y) else (y, x)) |> Set.singleton |> RangeSet

    /// Combines two `RangeSet`s into a new one.
    /// Please note that combining `RangeSet`s may be not very efficient as duplicate elements might exist.
    /// It is advised to combine `SetEx`es.
    let (++) (RangeSet x) (RangeSet y) = (x, y) ||> Set.union |> RangeSet

    /// Combines many `RangeSet`s into one.
    /// Please note that combining `RangeSet`s may be not very efficient as duplicate elements might exist.
    /// It is advised to combine `SetEx`es.
    let concat x = x |> Seq.map (fun (RangeSet x) -> x) |> Set.unionMany |> RangeSet

    /// Checks if a `RangeSet` contains an item.
    let contains (RangeSet s) x = s |> Set.exists (fun (a, b) -> x >= a && x <= b)

    /// Converts a `RangeSet` of characters to an F# set of characters that includes all its elements.
    let toCharSet (RangeSet x): char Set = x |> Seq.collect (fun (a, b) -> [a .. b]) |> set

    /// Converts an F# set to a `RangeSet`.
    /// To make the function generic, a function that returns the successor of a value is required.
    let ofSet fNext x =
        let rec impl first curr chars =
            match chars with
            | [] -> [first, curr]
            | x :: xs when x = fNext curr -> impl first x xs
            | x :: xs -> (first, curr) :: impl x x xs
        x |> Set.toList |>
        function
        | [] -> Set.empty |> RangeSet
        | x :: xs -> impl x x xs |> set |> RangeSet

    /// Converts an F# set of characters to a `RangeSet` of characters.
    let inline ofCharSet x = ofSet ((+) '\001') x

/// An unordered set that is either represented as an F# set or a `RangeSet`.
type SetEx<'a> when 'a: comparison =
    /// The set is represented as a `RangeSet`
    | Range of 'a RangeSet
    /// The set is represented as an F# set.
    | Set of 'a Set

/// Functions to work with `SetEx`es.
module SetEx =

    /// Maps the content of a `SetEx` with a different function depending on its internal representation.
    let inline tee fRange fSet =
        function
        | Range x -> fRange x
        | Set x -> fSet x

    /// Converts a `SetEx` to an F# set.
    let toCharSet = tee RangeSet.toCharSet id

    /// Converts a `SetEx` to a `RangeSet`.
    /// To make the function generic, a function that returns the successor of a value is required.
    let toRangeSet fSuccessor = tee id (RangeSet.ofSet fSuccessor)

    /// Converts a `SetEx` of characters to a `RangeSet` of characters.
    let toRangeCharSet = toRangeSet ((+) '\001')

    /// Checks if a `SetEx` contains an element.
    let contains x = tee (flip RangeSet.contains x) (Set.contains x)

    /// Unites two character `SetEx`es.
    /// The result is represented as an F# set.
    let (+) x1 x2 = toCharSet x1 + toCharSet x2 |> Set

    /// Subtracts the second character `SetEx` from the first.
    /// The result is represented as an F# set.
    let (-) x1 x2 = toCharSet x1 - toCharSet x2 |> Set

/// Some utilities to work with `SetEx`es, mainly for building grammars.
module SetUtils =

    /// Constructs a `SetEx` from the characters of the given string.
    let inline charSet (x: string) = x |> set |> Set

    /// Constructs a `SetEx` that contains the characters in the given ranges, inclusive.
    let inline range x = x |> set |> RangeSet |> Range
