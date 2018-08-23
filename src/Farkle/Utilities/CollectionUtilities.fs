// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Monads.StateResult
open System.IO

/// Functions on the standard F# `list` that mostly deal with the `StateResult` and `State` monads.
module List =

    /// The simple list cons operator.
    let cons x xs = x :: xs

    /// Returns a list with its last element removed.
    /// It should be called `init`, but there's already a function with that name.
    let skipLast x = x |> List.take (x.Length - 1)

    /// Returns a list with all its elements existing.
    let allSome x =
        let f x xs =
            match x, xs with
            | Some x, Some xs -> Some (x :: xs)
            | _ -> None
        List.foldBack f x (Some [])

/// Functions to work with sequences.
module Seq =

    /// Creates a lazily evaluated sequence of bytes from a stream with the option to dispose the stream when it ends.
    let ofByteStream disposeOnFinish (s: Stream) =
        let rec impl () = seq {
            match s.ReadByte() with
            | -1 -> if disposeOnFinish then s.Dispose()
            | x ->
                yield byte x
                yield! impl()
        }
        impl()

    /// Creates a lazily evaluated sequence of characters from a stream with the option to dispose the stream when it ends.
    /// The character encoding is automatically detected.
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
