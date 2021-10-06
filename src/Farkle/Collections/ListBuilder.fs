// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open FSharp.Core.CompilerServices

// Efficiently builds an F# list by adding elements to its end.
// Minimally implements the ICollection interface just to be usable
// by the manyCollection builder operator.
[<Sealed>]
type internal ListBuilder<'T>() =
    let mutable collector = ListCollector()
    static let moveToListFunc = Func<ListBuilder<'T>,_>(fun xs -> xs.MoveToList())
    member this.MoveToList() = collector.Close()
    static member MoveToListDelegate = moveToListFunc
    interface IEnumerable with
        member _.GetEnumerator() = raise (NotSupportedException())
    interface IEnumerable<'T> with
        member _.GetEnumerator() = raise (NotSupportedException())
    interface ICollection<'T> with
        member _.Count = raise (NotSupportedException())
        member _.IsReadOnly = raise (NotSupportedException())
        member _.Add x = collector.Add x
        member _.Clear() = raise (NotSupportedException())
        member _.Contains _ = raise (NotSupportedException())
        member _.CopyTo(_, _) = raise (NotSupportedException())
        member _.Remove _ = raise (NotSupportedException())
