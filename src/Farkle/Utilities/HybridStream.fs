// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Monads.StateResult
open FSharpx.Collections
open System.Collections
open System.Collections.Generic

type HybridStream<'T> =
    | Lazy of 'T LazyList
    | Eager of 'T list
    member private x.GetEnumeratorImpl() =
        match x with
            | Lazy x -> (x :> IEnumerable<_>).GetEnumerator()
            | Eager x -> (x :> IEnumerable<_>).GetEnumerator()
    interface IEnumerable<'T> with
        member x.GetEnumerator() = x.GetEnumeratorImpl()
    interface IEnumerable with
        member x.GetEnumerator() = x.GetEnumeratorImpl() :> IEnumerator

module HybridStream =

    module L = List
    module LL = LazyList
    // I yesterday learned about this trick, and I love it!

    let empty = Eager []

    let inline either fLazy fEager = function | Lazy x -> fLazy x | Eager x -> fEager x

    let inline tee fLazy fEager = either (fLazy >> Lazy) (fEager >> Eager)

    let (|HSCons|HSNil|) x =
        either
            (function | LL.Cons (x, xs) -> HSCons (x, Lazy xs) | LL.Nil -> HSNil)
            (function | x :: xs -> HSCons (x, Eager xs) | [] -> HSNil)
            x

    let isEmpty x = either LL.isEmpty L.isEmpty x

    let hasItems x = x |> isEmpty |> not

    let map f = tee (LL.map f) (L.map f)

    let splitAt count = either (fun x -> LL.split x count |> (fun (res, rest) -> res, Lazy rest)) (L.splitAt count >> (fun (res, rest) -> res, Eager rest))

    let takeOne() = sresult {
        let! s = get
        match s with
        | HSNil -> return! fail EOF
        | HSCons (x, rest) ->
            do! put rest
            return x
    }

    let takeM count = sresult {
        match count with
        | x when x < 0 -> return! fail TookNegativeItems
        | 0 -> return []
        | count ->
            let! x, rest = get <!> splitAt count
            do! put rest
            return x
    }

    let rec takeSafe n =
        function
        | _ when n = 0u -> []
        | HSNil -> []
        | HSCons (x, xs) -> x :: takeSafe (n - 1u) xs

    let ofSeq lazyLoad =
        if lazyLoad then
            LL.ofSeq >> Lazy
        else
            L.ofSeq >> Eager