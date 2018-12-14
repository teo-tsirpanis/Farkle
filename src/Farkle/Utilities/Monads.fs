// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads

open System
open System.Diagnostics

/// The well-known State monad.
type State<'s, 't> = 's -> ('t * 's)

/// [omit]
/// It doesn't need documentation. 😜
module State =

    let inline run (x: State<_,_>) = x
    let inline map f (m: State<_,_>): State<_,_> = fun s -> let (a, s') = m s in (f a, s')
    let inline (<!>) f x = map x f
    let inline apply (f: State<_,_>) (x: State<_,_>): State<_,_> = fun s -> let (f', s1) = f s in let (x', s2) = x s1 in (f' x', s2)
    let inline (<*>) f x = apply f x
    let inline bind f (m: State<_,_>): State<_,_> = fun s -> let (a, s') = m s in run (f a) s'
    let inline (>>=) result f = bind f result
    let inline returnM x = fun s0 -> x, s0
    let ignore x = map ignore x

    let inline eval (sa: State<_,_>) s = fst (sa s)
    let inline exec (sa: State<_,_>) s = snd (sa s)

    let get: State<_,_> = fun s -> (s, s)

    /// Replace the state inside the monad.
    let inline put x: State<_,_> = fun _ -> ((), x)

    type StateBuilder() =
        member inline __.Zero() = returnM ()
        member inline __.Bind(m, f) = bind f m
        member inline __.Return a = returnM a
        member inline __.ReturnFrom(x) = x
        member inline __.Combine (a, b) = bind b a
        member inline __.Delay f = f
        member inline __.Run f = f ()
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

/// A combination of the `Result` and `State` monads.
/// F# has no monad transformers, so it was manually done.
type  StateResult<'TSuccess, 'TState, 'TError> = State<'TState, Result<'TSuccess, 'TError>>

/// [omit]
/// It doesn't need documentation. 😜
module StateResult =

    let inline run (m: StateResult<_,_,_>) = State.run m
    let inline map f (m: StateResult<_,_,_>): StateResult<_,_,_> = State.map (Result.map f) m
    let inline (<!>) f x = map x f
    let inline bind f (m:StateResult<_,_,_>): StateResult<_,_,_> =
        fun s0 ->
            match m s0 with
            | (Ok x, s1) ->
                let q = f x
                let newResult, newState = q s1
                newResult, newState
            | (Error x, s) -> Error x, s
    let inline (>>=) result f = bind f result
    let inline apply f m = f >>= (fun f -> m >>= f)
    let inline (<*>) f x = apply f x
    let inline returnM x: StateResult<_,_,_> = x |> Ok |> State.returnM
    let ignore x = map (ignore) x

    let inline eval (sa: StateResult<_,_,_>) s = State.eval sa s
    let inline exec (sa: StateResult<_,_,_>) s = State.exec sa s

    let inline liftState x: StateResult<_,_,_> = x |> State.map Ok
    let inline liftResult x: StateResult<_,_,_> = x |> State.returnM

    let inline fail message = message |> Error |> liftResult

    let inline mapFailure f (m: StateResult<_,_,_>): StateResult<_,_,_> = m |> State.map (Result.mapError f)

    let get: StateResult<_,_,_> = fun s0 -> Ok s0, s0

    let inline put x: StateResult<_,_,_> = fun _ -> Ok(), x

    type StateResultBuilder() =
        member inline __.Zero() = returnM ()
        member inline __.Bind(m, f) = bind f m
        member inline __.Return a = returnM a
        member inline __.ReturnFrom(x) = x
        member inline __.Combine (a, b) = bind b a
        member inline __.Delay f = f
        member inline __.Run f = f ()
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

/// [omit]
module Maybe =

    [<DebuggerNonUserCode>]
    type MaybeBuilder() =
        member __.Zero() = Some ()
        member __.Bind(m, f) = Option.bind f m
        member __.Return a = Some a
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = Option.bind b a
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
                Option.bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    let maybe = MaybeBuilder()