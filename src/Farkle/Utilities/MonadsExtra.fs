// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads.Extra

open Farkle.Monads

/// Functions for working with `EndlessProcess<'T>`.
[<RequireQualifiedAccess>]
module State =


    /// Creates an sequence by continuously running the given `State` with the given initial state,
    /// and ending until the result of a computation satisfies the given predicate.
    let toSeq fShouldEndAfterThat stateM initialState =
        let mutable doFinish = false
        let impl s =
            let x = State.run stateM s
            if doFinish then
                None
            else
                doFinish <- fShouldEndAfterThat <| fst x
                Some x
        Seq.unfold impl initialState

    let runOver m1 m2 s1: State<_,_> = fun s2 ->
        let rec impl s1 s2 =
            let x1, s1 = State.run m1 s1
            match State.run (m2 x1) s2 with
            | Some x2, s2 -> x2, s2
            | None, s2 -> impl s1 s2
        impl s1 s2

    let runOverSeq m (xs: _ seq): State<_,_> = fun s ->
        let mutable x = None
        let mutable s = s
        use xs = xs.GetEnumerator()
        while xs.MoveNext() && x.IsNone do
            let x', s' = State.run (m xs.Current) s
            x <- x'
            s <- s'
        x, s
