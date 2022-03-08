// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle.Common
open System
open System.Buffers
open System.Diagnostics
open System.Runtime.CompilerServices

[<AutoOpen>]
module private StackNeoThrowHelpers =

    let throwEmptyStack() =
        invalidOp "The stack is empty."
        |> ignore

    let throwInvalidInitialCapacity (initialCapacity: int) =
        ArgumentOutOfRangeException(nameof initialCapacity, initialCapacity,
            "Invalid initial capacity.")
        |> raise
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
[<Struct; IsByRefLike; DebuggerDisplay("Count = {Count}")>]
type internal StackNeo<'T> =

    val mutable private items: 'T Span
    val mutable private pooledArray: 'T [] MaybeNull
    val mutable private size: int

    new(span: 'T Span) = {items = span; pooledArray = MaybeNull.nullValue; size = 0}

    new(initialCapacity) =
        if initialCapacity <= 0 then
            throwInvalidInitialCapacity initialCapacity
        let pooledArray = ArrayPool.Shared.Rent(initialCapacity)
        {items = pooledArray.AsSpan(); pooledArray = MaybeNull pooledArray; size = 0}

    /// The number of elements on the stack.
    member this.Count = this.size

    /// All the stack's items. The last item of
    /// the span is the most recent item pushed.
    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member this.AllItems = this.items.Slice(0, this.size)

    /// Pushes an item to the stack.
    member this.Push x =
        let size = this.size
        if size = this.items.Length then
            let newPooledArray = ArrayPool.Shared.Rent(if size <> 0 then size * 2 else 4)
            this.AllItems.CopyTo(Span(newPooledArray))
            let oldPooledArray = this.pooledArray
            if oldPooledArray.HasValue then
                if Reflection.isReferenceOrContainsReferences<'T> then
                    oldPooledArray.ValueUnchecked.AsSpan().Clear()
                ArrayPool.Shared.Return(oldPooledArray.ValueUnchecked)
            this.pooledArray <- MaybeNull newPooledArray
            this.items <- newPooledArray.AsSpan()
        this.items.[size] <- x
        this.size <- size + 1

    member this.Pop() =
        let size = this.size
        if size = 0 then
            throwEmptyStack()

        let resultRef = &this.items.[size - 1]
        this.size <- size - 1

        let result = resultRef
        if Reflection.isReferenceOrContainsReferences<'T> then
            resultRef <- Unchecked.defaultof<_>
        result

    /// Pops a specified amount of items from the stack.
    /// If more items than those on the stack are requested
    /// to be popped, the stack will become empty.
    member this.PopMany itemsToPop =
        if uint32 itemsToPop > uint32 this.size then
            throwPopMany itemsToPop
        let newsize = this.size - itemsToPop
        // Allow any popped references to be garbage collected.
        if Reflection.isReferenceOrContainsReferences<'T> then
            this.items.Slice(newsize, itemsToPop).Clear()
        this.size <- newsize

    member this.Clear() =
        if this.size <> 0 then
            if Reflection.isReferenceOrContainsReferences<'T> then
                this.AllItems.Clear()
            this.size <- 0

    member this.Dispose() =
        this.Clear()
        let pooledArray = this.pooledArray
        if pooledArray.HasValue then
            ArrayPool.Shared.Return(pooledArray.ValueUnchecked)
        this.items <- Span.Empty

    /// Gets the topmost item from the stack.
    member this.Peek() =
        if this.size = 0 then
            throwEmptyStack()
        this.items.[this.size - 1]

    /// Gets the `index`th item from the top of the stack.
    member this.Peek indexFromTheEnd =
        if uint32 indexFromTheEnd >= uint32 this.size then
            throwPeek indexFromTheEnd
        this.items.[this.size - 1 - indexFromTheEnd]

    /// Gets the top items from the stack, ordered by
    /// the time they were added in ascending order.
    member this.PeekMany numberOfItems =
        if uint32 numberOfItems > uint32 this.size then
            throwPeekMany numberOfItems
        Span.op_Implicit(this.items.Slice(this.size - numberOfItems, numberOfItems))
