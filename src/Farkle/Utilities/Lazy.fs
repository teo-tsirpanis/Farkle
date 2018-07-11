// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

/// Like `LazyFeedbackProcess`, except that this one never ends.
type EndlessProcess<'T> = EndlessProcess of 'T * EndlessProcess<'T> Lazy

/// Functions for working with `EndlessProcess<'T>`.
module EndlessProcess =

    open Farkle.Monads

    /// Creates an `EndlessProcess<'T>` by continuously running the given `State` with the given initial state.
    let ofState stateM initialState =
        let rec impl currState =
            let result, nextState = State.run stateM currState
            EndlessProcess (result, lazy(impl nextState))
        impl initialState