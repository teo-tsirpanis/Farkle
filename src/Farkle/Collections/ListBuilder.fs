// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices

type private MutableListWrapper<'T> private() =
    [<DefaultValue>] val mutable head: 'T
    [<DefaultValue>] val mutable tail: 'T list

type internal ListBuilder<'T>() =
    static let mkEmpty() =
#if MODERN_FRAMEWORK
        RuntimeHelpers
#else
        Runtime.Serialization.FormatterServices
#endif
            .GetUninitializedObject typeof<'T list>
        :?> 'T list
    let mutable head = mkEmpty()
    let mutable tail = head
    static let moveToListFunc = Func<ListBuilder<'T>,_>(fun xs -> xs.MoveToList())
    member this.MoveToList() =
        let list = head
        (this :> ICollection<_>).Clear()
        list
    static member MoveToListDelegate = moveToListFunc
    interface IEnumerable with
        member _.GetEnumerator() = (head :> IEnumerable).GetEnumerator()
    interface IEnumerable<'T> with
        member _.GetEnumerator() = (head :> _ seq).GetEnumerator()
    interface ICollection<'T> with
        member _.Count = head.Length
        member _.IsReadOnly = false
        member _.Add x =
            let newTail = mkEmpty()
            let mutList = Unsafe.As<MutableListWrapper<'T>> tail
            mutList.head <- x
            mutList.tail <- newTail
            tail <- newTail
        member _.Clear() =
            head <- mkEmpty()
            tail <- head
        member _.Contains _ = raise (NotImplementedException())
        member _.CopyTo(_, _) = raise (NotImplementedException())
        member _.Remove _ = raise (NotImplementedException())
