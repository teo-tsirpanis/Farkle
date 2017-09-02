// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Chessie.ErrorHandling
open FSharpx.Collections
open Farkle.Monads
open Farkle.Monads.StateResult
open System.IO
open System.Text

/// What can go wrong with a list operation
type ListError =
    /// The list reached its end.
    | EOF
    /// How could you❗
    /// I expected that the list would be single. 😭
    | ExpectedSingle
    /// You tried to take a negative number of items from the list.
    | TookNegativeItems

/// Functions on lists that mostly deal with the `StateResult` and `State` monads.
/// The type of the actual list is subject to change as long as the public API remains stable.
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

/// Functions to work with sequences.
module Seq =

    /// Makes pairs of two from a sequence.
    /// For example, `pairs [0;1;2;3]` becomes `[(0, 1); (2, 3)]`
    /// If there is an odd number of elements, the last is discarded.
    let pairs x =
        x
        |> Seq.chunkBySize 2
        |> Seq.map (fun x -> Seq.head x, x |> Seq.tail |> Seq.exactlyOne)

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
    let ofCharStream disposeOnFinish stream =
        let r = new StreamReader(stream, Encoding.UTF8, true, 1024, disposeOnFinish)
        let rec impl() = seq {
            match r.Read() with
            | -1 -> r.Dispose()
            | x ->
                yield char x
                yield! impl()
        }
        impl()

/// Functions to work with `RandomAccessList`s.
module RandomAccessList =

    /// If the list in the state has only one element, it is returned.
    /// Otherwise, `ExpectedSingle` is returned.
    let exactlyOne =
        function
        | RALCons(x, RALNil) -> ok x
        | _ -> Trial.fail ExpectedSingle
