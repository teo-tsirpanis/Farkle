// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices

[<Sealed>]
type private MutableListWrapper<'T> private() =
    [<DefaultValue>] val mutable head: 'T
    [<DefaultValue>] val mutable tail: 'T list

// Efficiently builds an F# list by adding elements to its end.
// Minimally implements the ICollection interface just to be usable
// by the manyCollection builder operator. Implementation based on
// https://github.com/krauthaufen/DawnSharp/blob/master/src/Armadillo/Tools/ListBuilder.fs.
[<Sealed>]
type internal ListBuilder<'T>() =
    static let unsafeSetTail (x: 'T list) (tail: 'T list) =
        if obj.ReferenceEquals(x, ([]: 'T list)) then
            invalidOp "Attempting to mutate the empty list singleton. Please report it on GitHub."
        Unsafe.As<MutableListWrapper<'T>>(x).tail <- tail
    let mutable head = []
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
            let newTail = [x]
            match head with
            | [] ->
                head <- newTail
                tail <- head
            | _ ->
                unsafeSetTail tail newTail
                tail <- newTail
        member _.Clear() =
            head <- []
            tail <- head
        member _.Contains _ = raise (NotImplementedException())
        member _.CopyTo(_, _) = raise (NotImplementedException())
        member _.Remove _ = raise (NotImplementedException())
