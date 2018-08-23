// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Monads.StateResult
open System.IO

/// What can go wrong with a list operation
type ListError =
    /// The list reached its end.
    | EOF
    /// You tried to take a negative number of items from the list.
    | TookNegativeItems

/// Functions on the standard F# `list` that mostly deal with the `StateResult` and `State` monads.
module List =

    /// The simple list cons operator.
    let cons x xs = x :: xs

    /// Returns a list with its last element removed.
    /// It should be called `init`, but there's already a function with that name.
    let skipLast x = x |> List.take (x.Length - 1)

    /// Checks whether the list has any item.
    let hasItems x = x |> List.isEmpty |> not

    /// Takes the first element of the list in the state and leaves the rest of them.
    /// It fails with an `EOF` if the list is empty.
    let takeOne() = sresult {
        let! s = get
        match s with
        | [] -> return! fail EOF
        | x :: rest ->
            do! put rest
            return x
    }

    /// Takes the first `count` elements of the list in the state and leaves the rest of them.
    let rec takeM count = sresult {
        match count with
        | x when x < 0 -> return! fail TookNegativeItems
        | 0 -> return []
        | count ->
            let! x, rest = get <!> List.splitAt count
            do! put rest
            return x
    }

    /// Skips the first `count` elements of the list in the state and leaves the rest of them.
    let skip count = count |> takeM |> ignore

    /// Takes at most `n` elements from a list.
    /// Returns an empty list on failure.
    let rec takeSafe n =
        function
        | _ when n <= 0 -> []
        | [] -> []
        | x :: xs -> x :: takeSafe (n - 1) xs

    let hasOneItem = function | [_] -> true | _ -> false

    /// Returns a list with all its elements existing.
    let allSome x =
        let f x xs =
            match x, xs with
            | Some x, Some xs -> Some (x :: xs)
            | _ -> None
        List.foldBack f x (Some [])

/// Functions to work with sequences.
module Seq =

    /// Makes pairs of two from a sequence.
    /// For example, `pairs [0;1;2;3]` becomes `[(0, 1); (2, 3)]`
    /// If there is an odd number of elements, the last is discarded.
    let pairs x =
        x
        |> Seq.chunkBySize 2
        |> Seq.map (fun x -> x.[0], x.[1])

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
