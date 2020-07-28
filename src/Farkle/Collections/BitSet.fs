// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Buffers
open System.Collections
open System.Collections.Generic
open System.Text

type private BitField = uint64

type BitSetEnumerator internal(data: BitField, extra: BitField []) =
    let mutable nextItem = -1
    let mutable currentField = data
    let mutable currentFieldIndex = -1
    let rec moveNext() =
        if currentField = 0UL then
            if currentFieldIndex = extra.Length - 1 then
                false
            else
                currentFieldIndex <- currentFieldIndex + 1
                currentField <- extra.[currentFieldIndex]
                nextItem <- (currentFieldIndex + 1) * 64 - 1
                moveNext()
        else
            nextItem <- nextItem + 1
            let isSet = currentField % 2UL
            currentField <- currentField / 2UL
            isSet = 1UL || moveNext()
    member _.MoveNext() = moveNext()
    member _.Current = nextItem
    interface IDisposable with
        member _.Dispose() = ()
    interface IEnumerator with
        member _.Current = box nextItem
        member _.MoveNext() = moveNext()
        member _.Reset() =
            nextItem <- -1
            currentField <- data
            currentFieldIndex <- -1
    interface IEnumerator<int> with
        member _.Current = nextItem

[<Struct; CustomEquality; CustomComparison>]
/// An immutable and memory-efficient set type that stores integers, each taking one bit.
type BitSet private(data: BitField, extra: BitField []) =
    static let emptyArray = Array.empty<BitField>
    static let newArray length = if length = 0 then emptyArray else Array.zeroCreate length
    static let argOutOfRange paramName =
        raise(ArgumentOutOfRangeException(paramName, "BitSet cannot store negative numbers."))
    static let emptyBitSet = BitSet(0UL, emptyArray)
    static let [<Literal>] bitFieldSize = 64
    /// An empty bit set.
    static member Empty = &emptyBitSet
    /// Creates a bit set from a sequence of numbers.
    /// If the sequence has any number less than zero
    /// it will throw an exception.
    static member CreateRange (numbers: _ seq) =
        match numbers with
        | :? BitSet as bs -> bs
        | _ ->
            let numbers = Array.ofSeq numbers
            Array.Sort numbers
            if Array.isEmpty numbers then
                emptyBitSet
            else
                let highestBit = numbers.[numbers.Length - 1]
                let mutable data = 0UL
                let extra = newArray (highestBit / bitFieldSize)
                for i in numbers do
                    if i < 0 then
                        argOutOfRange "numbers"
                    if i < bitFieldSize then
                        data <- data ||| (1UL <<< i)
                    else
                        let idx, ofs = Math.DivRem(i, bitFieldSize)
                        let cell = &extra.[idx - 1]
                        cell <- cell ||| (1UL <<< ofs)
                BitSet(data, extra)
    /// Creates a bit set containing only one number.
    static member Singleton(x) =
        if x < 0 then
            argOutOfRange "x"
        if x < bitFieldSize then
            BitSet(1UL <<< x, emptyArray)
        else
            let idx, ofs = Math.DivRem(x, bitFieldSize)
            let extra = Array.zeroCreate idx
            extra.[idx - 1] <- 1UL <<< ofs
            BitSet(0UL, extra)
    member private _.Data = data
    member private _.Extra = ReadOnlySpan extra
    /// Returns whether the bit set has the given number.
    member _.Contains(x) =
        if x < 0 then
            argOutOfRange "x"
        if x < bitFieldSize then
            data &&& (1UL <<< x) = 0UL
        else
            let idx = x / bitFieldSize
            let ofs = x % bitFieldSize
            idx < extra.Length && extra.[idx - 1] &&& (1UL <<< ofs) = 0UL
    /// Returns an enumerator for the set's elements.
    member _.GetEnumerator() = new BitSetEnumerator(data, extra)
    /// Returns the union of two bit sets.
    static member Union(x1: inref<BitSet>, x2: inref<BitSet>) =
        let data = x1.Data ||| x2.Data
        let extra = newArray (Math.Max(x1.Extra.Length, x2.Extra.Length))
        x1.Extra.CopyTo(extra.AsSpan())
        for i = 0 to x2.Extra.Length - 1 do
            let cell = &extra.[i]
            cell <- cell ||| x2.Extra.[i]
        BitSet(data, extra)
    /// Returns the intersection of two bit sets.
    static member Intersection(x1: inref<BitSet>, x2: inref<BitSet>) =
        let data = x1.Data &&& x2.Data
        let extraLength = Math.Min(x1.Extra.Length, x2.Extra.Length)
        let extraBuffer = ArrayPool.Shared.Rent(extraLength)
        x1.Extra.Slice(0, extraLength).CopyTo(extraBuffer.AsSpan())
        try
            let mutable extraLengthTrimmed = 0
            for i = 0 to extraLength - 1 do
                let x = x1.Extra.[i] &&& x2.Extra.[i]
                if x <> 0UL then extraLengthTrimmed <- i + 1
                extraBuffer.[i] <- x
            let extra = ReadOnlySpan(extraBuffer, 0, extraLengthTrimmed).ToArray()
            BitSet(data, extra)
        finally
            ArrayPool.Shared.Return(extraBuffer)
    /// Returns whether the two bit sets are equal.
    static member AreEqual(x1: inref<BitSet>, x2: inref<BitSet>) =
        x1.Data = x2.Data && x1.Extra.SequenceEqual(x2.Extra)
    /// Compares two bit sets by their largest element.
    static member Compare(x1: inref<BitSet>, x2: inref<BitSet>) =
        match compare x1.Extra.Length x2.Extra.Length with
        | 0 ->
            match x1.Extra.SequenceCompareTo(x2.Extra) with
            | 0 -> compare x1.Data x2.Data
            | x -> x
        | x -> x
    override x.Equals(x') =
        match x' with
        | :? BitSet as x' -> BitSet.AreEqual(&x, &x')
        | _ -> false
    override _.GetHashCode() = hash data ^^^ hash extra
    override _.ToString() =
        let sb = StringBuilder()
        sb.Append(data.ToString("X16")) |> ignore
        for i in extra do
            sb.Append('-').Append(i.ToString("X16")) |> ignore
        sb.ToString()
    interface IEquatable<BitSet> with
        member x.Equals(x') = BitSet.AreEqual(&x, &x')
    interface IComparable with
        member x.CompareTo(x') =
            let x' = x' :?> BitSet
            BitSet.Compare(&x, &x')
    interface IComparable<BitSet> with
        member x.CompareTo(x') = BitSet.Compare(&x, &x')
    interface IEnumerable with
        member x.GetEnumerator() = x.GetEnumerator() :> _
    interface IEnumerable<int> with
        member x.GetEnumerator() = x.GetEnumerator() :> _
