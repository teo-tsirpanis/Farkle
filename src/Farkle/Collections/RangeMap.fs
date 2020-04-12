// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections.Generic
open System.Collections.Immutable

[<Struct; CustomComparison; StructuralEquality>]
/// Αn closed intereval whose elements are assigned a value .
type internal RangeMapElement<'key,'a when 'key :> IComparable<'key>> = {
    /// The start of the intereval.
    KeyFrom: 'key
    /// The end of the intereval.
    KeyTo: 'key
    /// The corresponding value that gets assigned to the elements of the intereval.
    Value: 'a
}
with
    interface IComparable<RangeMapElement<'key,'a>> with
        member x.CompareTo({KeyTo = k2}) = x.KeyTo.CompareTo k2

/// A map data structure that works best when a continuous range of keys is assigned the same value.
/// It can also double as a set, when the value type is a unit.
type RangeMap<'key,'a when 'key :> IComparable<'key>> = private RangeMap of RangeMapElement<'key,'a> ImmutableArray
with
    member internal x.Elements = match x with RangeMap elements -> elements

/// Functions to create and use `RangeMap`s.
module RangeMap =

    /// An empty `RangeMap`.
    [<CompiledName("Empty")>]
    let empty<'key, 'a when 'key :> IComparable<'key>> : RangeMap<'key, 'a> =
        RangeMap ImmutableArray.Empty

    /// Looks up an element in a `RangeMap`, returning its corresponding value if it exists.
    [<CompiledName("TryFind")>]
    let tryFind k (RangeMap arr) =
        if arr.IsEmpty then
            ValueNone
        else
            let idx =
                // .NET's binary search function returns special integer values depending on the outcome.
                match arr.BinarySearch({KeyFrom = k; KeyTo = k; Value = Unchecked.defaultof<_>}) with
                // If it is positive, then an exact element was found in the array.
                | x when x >= 0 -> x
                // If it is the bitwise complement of the array's length, the requested element
                // is larger than the largest element in the array. In this case, we return the array's last element.
                | x when ~~~x = arr.Length -> arr.Length - 1
                // If it is negative, its bitwise complement signifies the next nearest element to be found.
                | x -> ~~~ x
            if arr.[idx].KeyFrom.CompareTo k <= 0 && k.CompareTo arr.[idx].KeyTo <= 0 then
                ValueSome <| arr.[idx].Value
            else
                ValueNone

    [<CompiledName("Map")>]
    /// Applies a function to each of the items of a `RangeMap`.
    let map f (RangeMap arr) =
        arr
        |> Seq.map (fun ({KeyFrom = k1; KeyTo = k2; Value = x}) -> {KeyFrom = k1; KeyTo = k2; Value = f x})
        |> (fun x -> x.ToImmutableArray() |> RangeMap)

    [<CompiledName("ContainsKey")>]
    /// Checks if the given `RangeMap` contains the given element.
    let containsKey k x = (tryFind k x).IsSome

    [<CompiledName("IsEmpty")>]
    /// Checks if the given `RangeMap` is empty.
    let isEmpty (RangeMap arr) = arr.IsEmpty

    let private consistencyCheck (arr: ImmutableArray.Builder<RangeMapElement<_,_>>) =
        let rec impl idx k0 =
            if idx < arr.Count then
                match arr.[idx] with
                | x when k0 < x.KeyFrom && x.KeyFrom <= x.KeyTo -> impl (idx + 1) x.KeyTo
                | _ -> false
            else
                true
        arr.Sort()
        if arr.Count <> 0 then
            match arr.[0] with
            | x when x.KeyFrom <= x.KeyTo -> impl 1 x.KeyTo
            | _ -> false
        else
            true

    /// Creates a `RangeMap` from an array of a range of keys and their corresponding value.
    /// The ranges are inclusive.
    /// The function may return `None` if some ranges overlap.
    [<CompiledName("CreateFromRanges")>]
    let ofRanges pairs =
        let arr = ImmutableArray.CreateBuilder(Array.length pairs)
        for (keys, value) in pairs do
            for (x1, x2) in keys do
                if x1 <= x2 then
                    arr.Add({KeyFrom = x1; KeyTo = x2; Value = value})
                else
                    arr.Add({KeyFrom = x2; KeyTo = x1; Value = value})
        match arr with
        | arr when consistencyCheck arr -> arr.ToImmutable() |> RangeMap |> Some
        | _ -> None

    /// Creates a `RangeMap` from a sequence of key-value pairs.
    /// The keys can be of any type that can has the notion of "one" and equality checking.
    /// The keys have to be sorted beforehand, or the resulting `RangeMap` will be broken.
    let ofSeqEx fSuccessor xs =
        if not <| Seq.isEmpty xs then
            let xs = Seq.sortBy (fun (KeyValue(k, _)) -> k) xs
            let (KeyValue(k, v)) = Seq.last xs
            Seq.foldBack (fun (KeyValue(k', v')) (kFrom, kTo, v, xs) ->
                if v = v' && kFrom = fSuccessor k' then
                    (k', kTo, v, xs)
                elif kFrom <> k' then
                    (k', k', v', {KeyFrom = kFrom; KeyTo = kTo; Value = v} :: xs)
                else
                    (kFrom, kTo, v, xs))
                xs (k, k, v, [])
            |> (fun (kFrom, kTo, v, xs) -> {KeyFrom = kFrom; KeyTo = kTo; Value = v} :: xs)
            |> ImmutableArray.CreateRange
            |> RangeMap
        else
            empty

    let inline ofSeq xs = ofSeqEx ((+) LanguagePrimitives.GenericOne) xs

    let toSeqEx (fRange: 'key -> 'key -> 'key seq) (RangeMap xs) = seq {
        for i = 0 to xs.Length - 1 do
            let {KeyFrom = kFrom; KeyTo = kTo; Value = v} = xs.[i]
            for k in fRange kFrom kTo do
                yield KeyValuePair(k, v)
    }

    let inline toSeq xs = toSeqEx (fun kFrom kTo -> seq {kFrom .. kTo}) xs
