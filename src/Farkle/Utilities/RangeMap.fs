// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open System

[<Struct; CustomEquality; CustomComparison>]
type internal RangePoint<'key,'a when 'key: comparison and 'key: equality> =
    | RangeInclusive of from:'key * _to:'key * value:'a
    | Singleton of key2:'key * value2:'a
    member x.Key =
        match x with
        | RangeInclusive (_, x, _)
        | Singleton (x, _) -> x
    interface IEquatable<RangePoint<'key,'a>> with
        member x.Equals(y) = x.Key = y.Key
    interface IComparable<RangePoint<'key,'a>> with
        member x.CompareTo(y) = compare x.Key y.Key
    interface IComparable with
        member x.CompareTo(y) =
            match y with
            | null -> 1
            | :? RangePoint<'key,'a> as y -> compare x.Key y.Key
            | _ -> invalidArg "y" "Argument must be a RangePoint"
    override x.GetHashCode() = hash x.Key
    override x.Equals(y) =
        match y with
        | :? RangePoint<'key,'a> as y -> x.Key = y.Key
        | _ -> false

[<Struct>]
/// A map data structure that works best when a continuous range of keys is assigned the same value.
/// It can also double as a set, when the value type is a unit.
type RangeMap<'key,'a when 'key: comparison and 'key: equality> = private RangeMap of RangePoint<'key,'a> []

module RangeMap =

    /// An empty `RangeMap`.
    [<CompiledName("Empty")>]
    let empty = RangeMap [| |]

    /// Looks up an element in a `RangeMap`, returning its corresponding value if it exists.
    [<CompiledName("TryFind")>]
    let tryFind k (RangeMap arr) =
        let idx =
            // .NET's binary search function returns special integer values depending on the outcome.
            match Array.BinarySearch(arr, Singleton (k, Unchecked.defaultof<_>)) with
            // If it is positive, then an exact element was found in the array.
            | x when x >= 0 -> x
            // If it is the bitwise complement of the array's length, the requested element
            // is larger than the largest element in the array. In this case, we return the array's last element.
            | x when ~~~x = arr.Length -> arr.Length - 1
            // If it is negative, its bitwise complement signifies the next nearest element to be found.
            | x -> ~~~ x
        match Array.tryItem idx arr with
        | Some(RangeInclusive (k1, k2, x)) when k1 <= k && k <= k2 -> Some x
        | Some(Singleton(k1, x)) when k1 = k -> Some x
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
            | RMCons(RangeInclusive(k1, k2, _), xs) when k0 < k1 && k1 < k2 -> impl k2 xs
            | RMCons(Singleton(k, _), xs) when k0 < k -> impl k xs
            | RMNil -> Some <| RangeMap arr
            | _ -> None
        Array.sortInPlace arr
        match ReadOnlyMemory arr with
        | RMCons(RangeInclusive(k1, k2, _), xs) when k1 < k2 -> impl k2 xs
        | RMCons(RangeInclusive(_), _) -> None
        | RMCons(Singleton(k, _), xs) -> impl k xs
        | RMNil -> Some <| RangeMap arr

    /// Creates a `RangeMap` from an array of a range of keys and their corresponding value.
    /// The ranges are inclusive.
    /// The function may return `None` if some ranges overlap.
    [<CompiledName("CreateFromRanges")>]
    let ofRanges pairs =
        let mapKeys (keys, value) =
            keys
            |> Array.map (fun (x1, x2) ->
                if x1 < x2 then
                    RangeInclusive (x1, x2, value)
                elif x1 = x2 then
                    Singleton (x1, value)
                else
                    RangeInclusive (x2, x1, value)
            )
        Array.collect mapKeys pairs |> consistencyCheck
