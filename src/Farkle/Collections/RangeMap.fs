// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices

[<Struct; IsReadOnly; CustomComparison; StructuralEquality>]
/// Α closed interval whose elements are assigned a value.
type RangeMapElement<'key,'a when 'key :> IComparable<'key>> = {
    /// The start of the interval.
    KeyFrom: 'key
    /// The end of the interval.
    KeyTo: 'key
    /// The value that gets assigned
    /// to the elements of the interval.
    Value: 'a
}
with
    interface IComparable<RangeMapElement<'key,'a>> with
        member x.CompareTo({KeyTo = k2}) = x.KeyTo.CompareTo k2

/// An associative data structure that assigns ranges of keys to a value.
type RangeMap<'key,'a when 'key :> IComparable<'key>> private(arr: RangeMapElement<'key,'a> []) =
    static let empty = RangeMap Array.empty<RangeMapElement<'key,'a>>
    let consistencyCheck() =
        let rec impl idx (k0: 'key) =
            if idx < arr.Length then
                match arr.[idx] with
                | x when k0.CompareTo x.KeyFrom < 0 && x.KeyFrom.CompareTo x.KeyTo <= 0 ->
                    impl (idx + 1) x.KeyTo
                | _ -> false
            else
                true
        Array.Sort arr
        if arr.Length <> 0 then
            match arr.[0] with
            | x when x.KeyFrom.CompareTo x.KeyTo <= 0 -> impl 1 x.KeyTo
            | _ -> false
        else
            true

    do
        if not <| consistencyCheck() then
            failwith "The range map is inconsistent: some ranges overlap."

    // Adapted from .NET's binary search function.
    let rec binarySearch lo hi k =
        if lo <= hi then
            let median = int ((uint32 hi + uint32 lo) >>> 1)
            match arr.[median].KeyTo.CompareTo k with
            | 0 -> median
            | x when x < 0 -> binarySearch (median + 1) hi k
            | _ -> binarySearch lo (median - 1) k
        else
            ~~~ lo

    /// Creates a range map from a sequence of range-value pairs.
    /// An exception will be raised if some ranges overlap.
    new (ranges: _ seq) =
        let ranges =
            ranges
            |> Seq.map (fun (kFrom: 'key, kTo, v) ->
                if kFrom.CompareTo kTo <= 0 then
                    {KeyFrom = kFrom; KeyTo = kTo; Value = v}
                else
                    {KeyFrom = kTo; KeyTo = kFrom; Value = v})
            |> Array.ofSeq
        RangeMap ranges

    /// Tries to find an element, returning its
    /// corresponding value, if it exists.
    member _.TryFind(k) =
        if Array.isEmpty arr then
            ValueNone
        else
            let idx =
                // The binary search function returns special integer values depending on the outcome.
                match binarySearch 0 (arr.Length - 1) k with
                // If it is positive, then an exact element was found in the array.
                | x when x >= 0 -> x
                // If it is negative, its bitwise complement signifies
                // the next nearest element to be found. We also limit
                // it to the highest index in the array.
                | x -> Math.Min(~~~x, arr.Length - 1)
            let element = &arr.[idx]
            if element.KeyFrom.CompareTo k <= 0 && k.CompareTo element.KeyTo <= 0 then
                ValueSome element.Value
            else
                ValueNone
    /// A read-only span containing the elements of the range map.
    member _.Elements = ReadOnlySpan(arr)
    /// Whether this range map is empty.
    member _.IsEmpty = Array.isEmpty arr
    /// Checks if the given element exists in this range map.
    member x.ContainsKey(k) = x.TryFind(k).IsSome
    /// An empty range map.
    static member Empty = empty
    interface IEnumerable with
        member _.GetEnumerator() = arr.GetEnumerator()
    interface IEnumerable<RangeMapElement<'key,'a>> with
        member _.GetEnumerator() = (arr :> _ seq).GetEnumerator()
    interface IReadOnlyCollection<RangeMapElement<'key,'a>> with
        member _.Count = arr.Length

[<CompiledName("RangeMapInternalUtils")>]
/// Additional functions to create and export range maps to sequences.
/// They are public due to compiler limitations.
/// Their use is not recommended by user code and they might be
/// removed or altered between non-major releases.
module RangeMap =

    let ofGroupedRanges xs =
        xs
        |> Seq.collect (fun (keys, value) ->
            Seq.map (fun (kFrom, kTo) -> kFrom, kTo, value) keys)
        |> RangeMap

    [<NoDynamicInvocation>]
    /// Creates a `RangeMap` from a sequence of key-value pairs.
    /// The keys can be of any type that can has the notion of "one" and equality checking.
    // This function can be used from F# like this:
    // let ofSeq xs = ofSeqEx
    // let rangeMap =
    //     mySequence
    //     |> Seq.map f
    //     |> ofSeq
    let inline ofSeqEx xs =
        if not <| Seq.isEmpty xs then
            let xs = Seq.sortBy (fun (KeyValue(k, _)) -> k) xs
            let (KeyValue(k, v)) = Seq.last xs
            Seq.foldBack (fun (KeyValue(k', v')) (kFrom, kTo, v, xs) ->
                if EqualityComparer.Default.Equals(v, v') && EqualityComparer.Default.Equals(kFrom, k + LanguagePrimitives.GenericOne) then
                    (k', kTo, v, xs)
                elif not (EqualityComparer.Default.Equals(kFrom, k')) then
                    (k', k', v', (kFrom, kTo, v) :: xs)
                else
                    (kFrom, kTo, v, xs))
                xs (k, k, v, [])
            |> (fun (kFrom, kTo, v, xs) -> (kFrom, kTo, v) :: xs)
            |> RangeMap
        else
            RangeMap.Empty

    [<NoDynamicInvocation>]
    let inline toSeqEx (rm: RangeMap<'key, _>) =
        rm
        |> Seq.collect (fun {KeyFrom = kFrom; KeyTo = kTo; Value = v} ->
            Seq.map (fun k -> KeyValuePair(k, v)) {kFrom .. kTo})
