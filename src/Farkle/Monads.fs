// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads

open Chessie.ErrorHandling
open System

type State<'s, 't> = State of ('s -> ('t * 's))

module State =

    let inline run (State x) = x
    let inline map f (State m) = State (fun s -> let (a, s') = m s in (f a, s'))
    let inline (<!>) f x = map x f
    let inline apply (State f) (State x) = State (fun s -> let (f', s1) = f s in let (x', s2) = x s1 in (f' x', s2))
    let inline (<*>) f x = apply f x
    let inline bind f (State m) = State (fun s -> let (a, s') = m s in run (f a) s')
    let inline (>>=) result f = bind f result
    let inline returnM x = (fun s0 -> x, s0) |> State
    let ignore x = map ignore x

    let inline eval (State sa) s = fst (sa s)
    let inline exec (State sa) s = snd (sa s)

    let get = State (fun s -> (s, s))

    /// Replace the state inside the monad.
    let inline put x = State (fun _ -> ((), x))

    type StateBuilder() =
        member __.Zero() = returnM ()
        member __.Bind(m, f) = bind f m
        member __.Return a = returnM a
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = bind b a
        member __.Delay f = f
        member __.Run f = f ()
        member __.TryWith (body, handler) =
            try
                body()
            with
            | e -> handler e
        member __.TryFinally (body, compensation) =
            try
                body()
            finally
                compensation()
        member x.Using(d:#IDisposable, body) =
            let result = fun () -> body d
            x.TryFinally (result, fun () ->
                match d with
                | null -> ()
                | d -> d.Dispose())
        member x.While (guard, body) =
            if not <| guard () then
                x.Zero()
            else
                bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    let state = StateBuilder()

type StateResult<'TSuccess, 'TState, 'TError> = StateResult of State<'TState, Result<'TSuccess, 'TError>>

module StateResult =

    let inline run (StateResult m) = State.run m
    let inline map f (StateResult m) = State.map (lift f) m |> StateResult
    let inline (<!>) f x = map x f
    let inline apply f (StateResult m) = State.apply (State.returnM(Trial.apply f)) m |> StateResult
    let inline (<*>) f x = apply f x
    let inline bind f (StateResult (State m)) =
        fun s0 ->
            match m s0 with
            | (Ok (x, messages), s) ->
               let (StateResult (State q)) = f x
               let newResult, newState = q s
               newResult |> mergeMessages messages, newState
            | (Bad x, s) -> Bad x, s
        |> State |> StateResult
    let inline (>>=) result f = bind f result
    let inline returnM x = x |> ok |> State.returnM |> StateResult
    let ignore x = map (ignore) x

    let inline liftState x = x |> State.map ok |> StateResult
    let inline liftResult x = x |> State.returnM |> StateResult

    let inline fail message = message |> fail |> liftResult

    let inline mapFailures f (StateResult m) =
        m |> State.map (function | Ok (x, errs) -> Ok (x, f errs) |Bad errs -> errs |> f |> Bad) |> StateResult

    let inline mapFailure f m = mapFailures (List.map f) m

    let get = StateResult(State(fun s0 -> Ok(s0, []), s0)) // Thank you F#'s type restrictions. 😠

    let inline put x = StateResult(State(fun s0 -> Ok((), []), x)) // Thank you F#'s type restrictions. 😠

    type StateResultBuilder() =
        member __.Zero() = returnM ()
        member __.Bind(m, f) = bind f m
        member __.Return a = returnM a
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = bind b a
        member __.Delay f = f
        member __.Run f = f ()
        member __.TryWith (body, handler) =
            try
                body()
            with
            | e -> handler e
        member __.TryFinally (body, compensation) =
            try
                body()
            finally
                compensation()
        member x.Using(d:#IDisposable, body) =
            let result = fun () -> body d
            x.TryFinally (result, fun () ->
                match d with
                | null -> ()
                | d -> d.Dispose())
        member x.While (guard, body) =
            if not <| guard () then
                x.Zero()
            else
                bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    let sresult = StateResultBuilder()