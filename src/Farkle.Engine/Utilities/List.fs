// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Chessie.ErrorHandling
open Farkle.Monads
open Farkle.Monads.StateResult
open System.IO

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

    /// Makes pairs of two from a list.
    /// For example, `pairs [0;1;2;3]` becomes `[(0, 1); (2, 3)]`
    /// If there is an odd number of elements, the last is discarded.
    let rec pairs =
        function
        | x1 :: x2 :: xs -> (x1, x2) :: pairs xs
        | _ -> []

    /// Returns a list with its last element removed.
    /// It should be called `init`, but there's already a function with that name.
    let skipLast x = x |> List.take (x.Length - 1)

    /// Creates a list of bytes from a stream.
    let ofByteStream s =
        let rec impl (s: Stream) = seq {
            match s.ReadByte() with
            | -1 -> ()
            | x ->
                yield byte x
                yield! impl s
        }
        s |> impl |> List.ofSeq

    /// Checks whether the list has any item.
    let hasItems x = x |> List.isEmpty |> not

    /// If the list in the state has only one element, it is returned.
    /// Otherwise, `ExpectedSingle` is returned.
    let exactlyOne =
        function
        | [x] -> ok x
        | _ -> Trial.fail ExpectedSingle

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
