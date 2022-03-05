// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open System.ComponentModel
open System.Diagnostics
open System.Runtime.CompilerServices

/// Α closed interval whose elements are assigned a value.
[<Struct; IsReadOnly; CustomComparison; StructuralEquality>]
[<DebuggerDisplay("{DebuggerDisplay,nq}")>]
type RangeMapElement<'TKey, [<Nullable(2uy)>] 'a when 'TKey :> IComparable<'TKey>> = {
    /// The start of the interval.
    KeyFrom: 'TKey
    /// The end of the interval.
    KeyTo: 'TKey
    /// The value that gets assigned
    /// to the elements of the interval.
    Value: 'a
}
with
    member private x.DebuggerDisplay =
        sprintf "[%A, %A] -> %A" x.KeyFrom x.KeyTo x.Value
    interface IComparable<RangeMapElement<'TKey,'a>> with
        member x.CompareTo({KeyTo = k2}) = x.KeyTo.CompareTo k2

/// An associative data structure that assigns ranges of keys to a value.
[<DebuggerTypeProxy("Farkle.DebugTypeProxies.RangeMapDebugProxy`2")>]
[<DebuggerDisplay("Count: {arr.Length}")>]
type RangeMap<'TKey, [<Nullable(0uy)>] 'TValue when 'TKey :> IComparable<'TKey>> private(arr: RangeMapElement<'TKey,'TValue> []) =
    static let empty = RangeMap (Array.Empty<RangeMapElement<'TKey,'TValue>>())
    let consistencyCheck() =
        let rec impl idx (k0: 'TKey) =
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
            |> Seq.map (fun (kFrom: 'TKey, kTo, v) ->
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
    member internal _.ElementsArray = arr
    /// Whether this range map is empty.
    member _.IsEmpty = Array.isEmpty arr
    /// Checks if the given element exists in this range map.
    member x.ContainsKey(k) = x.TryFind(k).IsSome
    member x.GetEnumerator() = x.Elements.GetEnumerator()
    /// An empty range map.
    static member Empty = empty
    interface IEnumerable with
        member _.GetEnumerator() = arr.GetEnumerator()
    interface IEnumerable<RangeMapElement<'TKey,'TValue>> with
        member _.GetEnumerator() = (arr :> _ seq).GetEnumerator()
    interface IReadOnlyCollection<RangeMapElement<'TKey,'TValue>> with
        member _.Count = arr.Length

/// This module is public due to compiler limitations.
/// Do not use it; it is subject to be removed or altered at any time.
[<CompiledName("RangeMapInternalUtils")>]
[<EditorBrowsable(EditorBrowsableState.Never)>]
module RangeMap =

    open LanguagePrimitives

    let ofGroupedRanges xs =
        xs
        |> Seq.collect (fun (keys, value) ->
            Seq.map (fun (kFrom, kTo) -> kFrom, kTo, value) keys)
        |> RangeMap

    /// Creates a `RangeMap` from a sequence of key-value pairs.
    /// The keys can be of any type that can has the notion of "one" and equality checking.
    // This function can be used from F# like this:
    // let ofSeq xs = ofSeqEx
    // let rangeMap =
    //     mySequence
    //     |> Seq.map f
    //     |> ofSeq
    [<NoDynamicInvocation>]
    let inline ofSeqEx xs =
        let xs = Array.ofSeq xs
        if not <| Array.isEmpty xs then
            Array.sortInPlaceBy (fun (KeyValue(k, _)) -> k) xs
            let (KeyValue(kLast, vLast)) = Array.last xs
            Array.foldBack (fun (KeyValue(kCurrent, vCurrent)) (kFrom, kTo, vExisting, xs) ->
                if EqualityComparer.Default.Equals(vExisting, vCurrent)
                    && EqualityComparer.Default.Equals(kFrom, kCurrent + GenericOne) then
                    (kCurrent, kTo, vExisting, xs)
                elif not (EqualityComparer.Default.Equals(kFrom, kCurrent)) then
                    (kCurrent, kCurrent, vCurrent, (kFrom, kTo, vExisting) :: xs)
                else
                    (kFrom, kTo, vExisting, xs))
                xs (kLast, kLast, vLast, [])
            |> (fun (kFrom, kTo, v, xs) -> (kFrom, kTo, v) :: xs)
            |> RangeMap
        else
            RangeMap.Empty

    [<NoDynamicInvocation>]
    let inline toSeqEx (rm: RangeMap< ^TKey, ^TValue>) = seq {
        for {KeyFrom = kFrom; KeyTo = kTo; Value = v} in rm do
            for k in {kFrom .. kTo} do
                yield KeyValuePair(k, v)
    }
