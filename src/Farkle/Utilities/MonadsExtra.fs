// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads.Extra

open Farkle.Monads
open System.Collections.Generic

/// Some more functions to work with `State`.
[<RequireQualifiedAccess>]
module State =


    /// Creates an endless sequence by continuously running the given `State` with the given initial state.
    let toSeq stateM initialState =
        let impl s = Some <| State.run stateM s
        Seq.unfold impl initialState

    /// Continuously runs a `State` object, whose result is given
    /// to a function, until the function's result contains some value.
    let runOver m1 m2 s1: State<_,_> = fun s2 ->
        let rec impl s1 s2 =
            let x1, s1 = State.run m1 s1
            match State.run (m2 x1) s2 with
            | Some x2, s2 -> x2, s2
            | None, s2 -> impl s1 s2
        impl s1 s2

    /// Gives each item of a sequence to a function that returns a `State` object, until this object returns some value.
    /// If the sequence ends, the last element will be continuously applied to the function.
    let runOverSeq (xs: _ seq) m: State<_,_> = fun s ->
        let rec impl (xs: IEnumerator<_>) s =
            // I didn't find anything on whether I can call ModeNext even when the enumeration is done.
            // I guess it would just return the last object of the collection.
            xs.MoveNext() |> ignore
            match State.run (m xs.Current) s with
            | Some x, s -> x, s
            | None, s -> impl xs s
        use xs = xs.GetEnumerator()
        impl xs s
