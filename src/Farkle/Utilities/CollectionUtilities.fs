// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Monads.StateResult
open System.IO

/// Functions to work with the standard F# `list`.
module List =

    /// The simple list cons operator.
    let inline cons x xs = x :: xs

    /// Returns a list with all its elements existing.
    let allSome x =
        let f x xs =
            match x, xs with
            | Some x, Some xs -> Some (x :: xs)
            | _ -> None
        List.foldBack f x (Some [])

    let popStack n x =
        let rec impl acc n x =
            match x with
            | x :: xs when n >= 1 -> impl (x :: acc) (n - 1) xs
            | x -> acc, x
        impl [] n x

    let popStackM optic count = sresult {
        let! (first, rest) = getOptic optic <!> popStack count
        do! setOptic optic rest
        return first
    }

/// Functions to work with sequences.
module Seq =

    /// Creates a lazily evaluated sequence of characters from a stream with the option to dispose the stream when it ends.
    let ofCharStream disposeOnFinish encoding stream =
        let r = new StreamReader(stream, encoding, true, 1024, disposeOnFinish)
        let rec impl() = seq {
            match r.Read() with
            | -1 -> r.Dispose()
            | x ->
                yield char x
                yield! impl()
        }
        impl()
