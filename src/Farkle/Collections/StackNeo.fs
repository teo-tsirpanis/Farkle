// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle.Common
open System
open System.Diagnostics

[<AutoOpen>]
module private StackNeoThrowHelpers =

    let throwEmptyStack() =
        invalidOp "The stack is empty."
        |> ignore

    let throwPopMany (itemsToPop: int) =
        ArgumentOutOfRangeException(nameof itemsToPop, itemsToPop,
            "Invalid number of items to pop.")
        |> raise
        |> ignore

    let throwPeek (indexFromTheEnd: int) =
        ArgumentOutOfRangeException(nameof indexFromTheEnd, indexFromTheEnd,
            "Cannot peek at the item at this index.")
        |> raise
        |> ignore

    let throwPeekMany (numberOfItems: int) =
        ArgumentOutOfRangeException(nameof numberOfItems, numberOfItems,
            "Cannot peek this number of items.")
        |> raise
        |> ignore

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
        if uint32 itemsToPop > uint32 size then
            throwPopMany itemsToPop
        size <- size - itemsToPop

    /// Gets the topmost item from the stack.
    member _.Peek() =
        if size = 0 then
            throwEmptyStack()
        items.[size - 1]

    /// Gets the `index`th item from the top of the stack.
    member _.Peek indexFromTheEnd =
        if uint32 indexFromTheEnd >= uint32 size then
            throwPeek indexFromTheEnd
        items.[size - 1 - indexFromTheEnd]

    /// Gets the top items from the stack, ordered by
    /// the time they were added in ascending order.
    member _.PeekMany numberOfItems =
        if uint32 numberOfItems > uint32 size then
            throwPeekMany numberOfItems
        ReadOnlySpan(items, size - numberOfItems, numberOfItems)
