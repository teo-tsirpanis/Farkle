// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open System
open System.Collections.Generic

[<Struct>]
/// A map data structure that works best when a continuous range of keys is assigned the same value.
/// It can also double as a set, when the value type is a unit.
type RangeMap<'key,'a when 'key: comparison> = private RangeMap of ('key * 'key * 'a) []

module RangeMap =

    open FastCompare

    let internal secondComparer = {
        new IComparer<_> with
            member __.Compare((_, x, _), (_, y, _)) = compare x y
    }

    /// An empty `RangeMap`.
    [<CompiledName("Empty")>]
    let empty = RangeMap [| |]

    /// Looks up an element in a `RangeMap`, returning its corresponding value if it exists.
    [<CompiledName("TryFind")>]
    let tryFind k (RangeMap arr) =
        let idx =
            // .NET's binary search function returns special integer values depending on the outcome.
            match Array.BinarySearch(arr, (k, k, Unchecked.defaultof<_>), secondComparer) with
            // If it is positive, then an exact element was found in the array.
            | x when x >= 0 -> x
            // If it is the bitwise complement of the array's length, the requested element
            // is larger than the largest element in the array. In this case, we return the array's last element.
            | x when ~~~x = arr.Length -> arr.Length - 1
            // If it is negative, its bitwise complement signifies the next nearest element to be found.
            | x -> ~~~ x
        match Array.tryItem idx arr with
        | Some((k1, k2, x)) when smallerOrEqual k1 k && smallerOrEqual k k2 -> Some x
        | None | Some _ -> None

    [<CompiledName("ContainsKey")>]
    /// Checks if the given `RangeMap` contains the given element.
    let containsKey k = tryFind k >> Option.isSome

    [<CompiledName("IsEmpty")>]
    /// Checks if the given `RangeMap` is empty.
    let isEmpty (RangeMap arr) = Array.isEmpty arr

    let internal consistencyCheck arr =
        let rec impl k0 =
            function
            | RMCons((k1, k2, _), xs) when smaller k0 k1 && smallerOrEqual k1 k2 -> impl k2 xs
            | RMNil -> Some <| RangeMap arr
            | _ -> None
        Array.sortInPlace arr
        match ReadOnlyMemory arr with
        | RMCons((k1, k2, _), xs) when smallerOrEqual k1 k2 -> impl k2 xs
        | RMCons(_, _) -> None
        | RMNil -> Some <| RangeMap arr

    /// Creates a `RangeMap` from an array of a range of keys and their corresponding value.
    /// The ranges are inclusive.
    /// The function may return `None` if some ranges overlap.
    [<CompiledName("CreateFromRanges")>]
    let ofRanges pairs =
        let mapKeys (keys, value) =
            keys
            |> Array.map (fun (x1, x2) ->
                if smallerOrEqual x1 x2 then
                    (x1, x2, value)
                else
                    (x2, x1, value)
            )
        Array.collect mapKeys pairs |> consistencyCheck
