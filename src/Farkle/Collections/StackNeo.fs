// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Diagnostics

/// A stack type that supports efficiently popping and peeking many items at once.
[<DebuggerDisplay("Count = {Count}")>]
type internal StackNeo<'T>() =
    let mutable items =
        let initialCapacity =
#if DEBUG
            1
#else
            64
#endif
        Array.zeroCreate<'T> initialCapacity
    let mutable size = 0

    /// The number of elements on the stack.
    member _.Count = size

    /// All the stack's items. The last item of
    /// the span is the most recent item pushed.
    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member _.AllItems = ReadOnlySpan(items, 0, size)

    /// Pushes an item to the stack.
    member _.Push x =
        if size = items.Length then
            Array.Resize(&items, items.Length * 2)
        items.[size] <- x
        size <- size + 1

    /// Pops a specified amount of items from the stack.
    /// If more items than those on the stack are requested
    /// to be popped, the stack will become empty.
    member _.PopMany itemsToPop =
        if itemsToPop >= 0 then
            size <- Math.Max(size - itemsToPop, 0)
        else
            ArgumentOutOfRangeException(nameof itemsToPop, itemsToPop,
                "Cannot pop a negative amount of items from the stack.")
            |> raise

    /// Gets the topmost item from the stack.
    member _.Peek() =
        if size <> 0 then
            items.[size - 1]
        else
            invalidOp "The stack is empty."

    /// Gets the `index`th item from the top of the stack.
    member _.Peek indexFromTheEnd =
        if indexFromTheEnd >= 0 && indexFromTheEnd < size then
            items.[size - 1 - indexFromTheEnd]
        else
            ArgumentOutOfRangeException(nameof indexFromTheEnd, indexFromTheEnd,
                "There are not enough items on the stack.")
            |> raise

    /// Gets the top items from the stack, ordered by
    /// the time they were added in ascending order.
    member _.PeekMany itemsToGet =
        let count =
#if MODERN_FRAMEWORK
            Math.Clamp(itemsToGet, 0, size)
#else
            if itemsToGet < 0 then
                0
            elif itemsToGet > size then
                size
            else
                itemsToGet
#endif
        ReadOnlySpan(items, size - count, count)
