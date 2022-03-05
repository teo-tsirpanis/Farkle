// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System
open System.Collections
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices

// Efficiently builds an F# list by adding elements to its end.
// Minimally implements the ICollection interface just to be usable
// by the manyCollection builder operator. Implementation based on
// https://github.com/krauthaufen/DawnSharp/blob/master/src/Armadillo/Tools/ListBuilder.fs.
[<Sealed>]
type internal ListBuilder<'T>() =
    let mutable builder = ListCollector<'T>()
    static let moveToListFunc = Func<ListBuilder<'T>,_>(fun xs -> xs.MoveToList())
    member this.MoveToList() = builder.Close()
    static member MoveToListDelegate = moveToListFunc
    interface IEnumerable with
        member _.GetEnumerator() = raise (NotImplementedException())
    interface IEnumerable<'T> with
        member _.GetEnumerator() = raise (NotImplementedException())
    interface ICollection<'T> with
        member _.Count = raise (NotImplementedException())
        member _.IsReadOnly = false
        member _.Add x = builder.Add x
        member _.Clear() = builder <- Unchecked.defaultof<_>
        member _.Contains _ = raise (NotImplementedException())
        member _.CopyTo(_, _) = raise (NotImplementedException())
        member _.Remove _ = raise (NotImplementedException())
