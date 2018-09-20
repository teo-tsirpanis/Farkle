// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// A set with a varying representation.
[<RequireQualifiedAccess>]
type SetEx<'a> when 'a: comparison =
    /// The set is represented as a list of ranges.
    | Range of ('a * 'a) list
    /// The set is represented as an F# set.
    | Set of 'a Set

/// Functions to work with lists of ranges.
[<RequireQualifiedAccess>]
module RangeSet =

    /// Returns whether the fiven item exists in the range defined by the given list, inclusive.
    /// In the list, the smallest member of the tuple must be first.
    let inline contains x xs = List.exists (fun (x1, x2) -> x >= x1 && x <= x2) xs

/// Functions to work between sets of different types.
[<RequireQualifiedAccess>]
module SetUtils =

    /// Converts an F# set to a list of ranges.
    let inline setToRanges x =
        let rec impl acc first curr x =
            match x with
            | [] -> (first, curr) :: acc
            | x :: xs when x = curr + LanguagePrimitives.GenericOne -> impl acc first x xs
            | x :: xs -> impl ((first, curr) :: acc) x x xs
        match Set.toList x with
        | [] -> []
        | x :: xs -> impl [] x x xs

    /// Converts a list of ranges to an F# set.
    let inline rangesToSet ranges = ranges |> Seq.collect (fun (x1, x2) -> if x1 < x2 then [x1..x2] else [x2..x1]) |> set

/// Functions to work with `SetEx`es.
[<RequireQualifiedAccess>]
module SetEx =

    /// Creates a `SetEx` from the given list of ranges.
    let inline ofRanges ranges =
        let items = SetUtils.rangesToSet ranges
        // The computational complexity of a range set is linear in terms of the number of ranges.
        let nRange = List.length ranges |> float
        // The computational complexity of a tree set is logarithmic in terms of the number of items.
        let nSet = (Set.count items |> float |> log) / (log 2.0)
        if nSet > nRange then
            SetEx.Set items
        else
            SetEx.Range ranges

    /// Maps the content of a `SetEx` with a different function depending on its internal representation.
    let inline tee fRange fSet =
        function
        | SetEx.Range x -> fRange x
        | SetEx.Set x -> fSet x

    /// Converts a `SetEx` to an F# set.
    let inline toSet x = tee SetUtils.rangesToSet id x

    /// Converts a `SetEx` to a list of ranges.
    let inline toRanges x = tee id SetUtils.setToRanges x

    /// Checks if a `SetEx` contains an element.
    let inline contains x = tee (RangeSet.contains x) (Set.contains x)
