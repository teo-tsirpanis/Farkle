// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// An endless, lazily evaluated sequence.
type EndlessProcess<'T> = EndlessProcess of ('T * EndlessProcess<'T>) Lazy

/// Functions for working with `EndlessProcess<'T>`.
[<RequireQualifiedAccess>]
module EndlessProcess =

    open Farkle.Monads

    /// Creates an `EndlessProcess<'T>` by continuously running the given `State` with the given initial state.
    let ofState stateM initialState =
        let rec impl currState = lazy (
            let result, nextState = State.run stateM currState
            result, impl nextState) |> EndlessProcess
        impl initialState

    let runOver m xs: State<_,_> = fun s ->
        let rec impl (EndlessProcess xs) s =
            let x, xs = xs.Value
            match State.run (m x) s with
            | Some x, s -> x, s
            | None, s -> impl xs s
        impl xs s
