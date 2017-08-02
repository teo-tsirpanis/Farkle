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

/// Functions on lists that mostly deal with the `StateResult` and `State` monads.
/// The type of the actual list is subject to change as long as the public API remains stable.
module List =

    /// Makes pairs of two from a list.
    /// For example, `pairs [0;1;2;3]` becomes `[(0, 1); (2, 3)]`
    /// If there is an odd number of elements, the last is discarded.
    let rec pairs =
        function
        | x1 :: x2 :: xs -> (x1, x2) :: pairs xs
        | _ -> []

    /// Returns a list with its last element removed.
    /// It should be called `init`, but there's already a function with that name.
    let skipLast =
        function
        | [] -> []
        | x -> x |> List.rev |> List.tail |> List.rev 

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
    /// Because there is no real way to fail (apart from NRE), the function returns a simple `State` monad.
    let hasItems() = State.get |> State.map (List.isEmpty >> not)

    /// Checks the length of the list in the state.
    /// Because there is no real way to fail (apart from NRE), the function returns a simple `State` monad.
    let length() = State.get |> State.map List.length

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
    let rec take count = sresult {
        match count with
        | 0 -> return []
        | count ->
            let! x = takeOne()
            let! rest = take <| count - 1
            return x :: rest
    }

    /// Skips the first `count` elements of the list in the state and leaves the rest of them.
    let skip count = count |> take |> ignore
