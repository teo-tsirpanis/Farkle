// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Chessie.ErrorHandling
open Farkle.Monads
open Farkle.Monads.StateResult

/// What can go wrong with a sequence operation
type SeqError =
    /// The sequence reached its end.
    | EOF
    /// You took 4 elements from a sequence with 3, or something like this.
    | InvalidLength
    /// How could you❗
    /// I expected that the sequence would be single. 😭
    | ExpectedSingle of actualLength: int

/// Functions on `seq`s that mostly work with the `StateResult` and `State` monads.
module Seq =

    /// Makes pairs of two from a sequence.
    /// For example, `pairs [0;1;2;3]` becomes `[(0, 1); (2, 3)]`
    /// If there is an odd number of elements, the last is discarded.
    let rec pairs x = seq {
        if not <| Seq.isEmpty x then
            let first, rest = Seq.head x, Seq.tail x
            if not <| Seq.isEmpty rest then
                let second, rest = Seq.head rest, Seq.tail rest
                yield first, second
                yield! pairs rest}

    /// Checks whether the sequence in the state is empty.
    /// Because there is no real way to fail (apart from NRE), the function returns a simple `State` monad.
    let isEmpty() = State.get |> State.map Seq.isEmpty

    /// Checks the length of the sequence in the state.
    /// Because there is no real way to fail (apart from NRE), the function returns a simple `State` monad.
    let length() = State.get |> State.map Seq.length

    /// Looks at the first element of the sequence in the state without modifying it.
    /// It fails with an `EOF` if the sequence is empty.
    let peekOne() = get >>= (Seq.tryHead >> failIfNone EOF >> liftResult)

    /// If the sequence in the state has one element, it is returned.
    /// Otherwise, `ExpectedSingle` is returned.
    /// Think of it as a functional monadic equivalent of `System.Linq.Enumerable.Single`.
    let single() = sresult {
        let! len = length() |> liftState
        if len = 1 then
            return! peekOne()
        else
            return! len |> ExpectedSingle |> fail
    }

    /// Takes the first element of the sequence in the state and leaves the rest of them.
    /// It fails with an `EOF` if the sequence is empty.
    let takeOne() = sresult {
        let! s = peekOne()
        do! get <!> Seq.tail >>= put
        return s
    }

    /// Looks at the first `count` elements of the sequence in the state without modifying it.
    /// It fails with an `InvalidLength` if you are taking more elements than those it has.
    let peek count = sresult {
        let! s = get
        if Seq.length s < count then
            return! fail InvalidLength
        else
            return s |> Seq.take count
    }

    /// Skips the first `count` elements of the sequence in the state without modifying it.
    /// It fails with an `InvalidLength` if you are skipping more elements than those it has.
    let peekNext count = sresult {
        let! s = get
        if Seq.length s < count then
            return! fail InvalidLength
        else
            return s |> Seq.skip count
    }

    /// Takes the first `count` elements of the sequence in the state and leaves the rest of them.
    /// It fails with an `InvalidLength` if you are taking more elements than those it has.
    let take count = sresult {
        let! s = get
        let! result = peek count
        do! peekNext count >>= put
        return result
    }

    /// Skips the first `count` elements of the sequence in the state and leaves the rest of them.
    /// It fails with an `InvalidLength` if you are skipping more elements than those it has.
    let skip count = sresult {
        do! peekNext count >>= put
    }
