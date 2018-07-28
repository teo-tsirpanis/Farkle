// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads

open System
open System.Diagnostics

/// The well-known State monad.
type [<Struct>] State<'s, 't> = State of ('s -> ('t * 's))

/// [omit]
/// It doesn't need documentation. 😜
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

    // "I will not use imperative code in F# again 😭"
    // |> Seq.replicate 100
    // |> Seq.iter (printfn"%s")
    let repeatM f times = state {
        let buf = new ResizeArray<_>(times + 0)
        for i = 0 to times - 1 do
            let! x = f
            buf.Add x
        return buf :> seq<_>
    }

    // "I will not use imperative code in F# again 😭"
    // |> Seq.replicate 100
    // |> Seq.iter (printfn"%s")
    let whileM f action =
        let buf = ResizeArray<_>()
        let rec impl() = state {
            let! x = f
            if x then
                let! y = action
                buf.Add y
                return! impl()
            else
                return buf :> seq<_>
        }
        impl()

    open Aether

    let inline getOptic optic = state {
        let! x = get
        return Optic.get optic x
    }

    let inline setOptic optic value = state {
        let! x = get
        return! Optic.set optic value x |> put
    }

    let inline mapOptic optic f = state {
        let! x = get
        let s: 'c -> 'c = Optic.map optic f
        return! s x |> put
    }

/// A combination of the `Result` and `State` monads.
/// F# has no monad transformers, so it was manually done.
type [<Struct>] StateResult<'TSuccess, 'TState, 'TError> = StateResult of State<'TState, Result<'TSuccess, 'TError>>

/// [omit]
/// It doesn't need documentation. 😜
module StateResult =

    let inline run (StateResult m) = State.run m
    let inline map f (StateResult m) = State.map (Result.map f) m |> StateResult
    let inline (<!>) f x = map x f
    let inline bind f (StateResult (State m)) =
        fun s0 ->
            match m s0 with
            | (Ok x, s1) ->
               let (StateResult (State q)) = f x
               let newResult, newState = q s1
               newResult, newState
            | (Error x, s) -> Error x, s
        |> State |> StateResult
    let inline (>>=) result f = bind f result
    let inline apply f m = f >>= (fun f -> m >>= f)
    let inline (<*>) f x = apply f x
    let inline returnM x = x |> Ok |> State.returnM |> StateResult
    let ignore x = map (ignore) x

    let inline eval (StateResult sa) s = State.eval sa s
    let inline exec (StateResult sa) s = State.exec sa s

    let inline liftState x = x |> State.map Ok |> StateResult
    let inline liftResult x = x |> State.returnM |> StateResult

    let inline fail message = message |> Error |> liftResult

    let inline mapFailure f (StateResult m) = m |> State.map (Result.mapError f) |> StateResult

    let get = StateResult(State(fun s0 -> Ok s0, s0)) // Thank you F#'s type restrictions. 😠

    let inline put x = StateResult(State(fun _ -> Ok(), x)) // Thank you F#'s type restrictions. 😠

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

    // "I will not use imperative code in F# again 😭"
    // |> Seq.replicate 100
    // |> Seq.iter (printfn"%s")
    let repeatM f times = sresult {
        let buf = new ResizeArray<_>(times + 0)
        for i = 0 to times - 1 do
            let! x = f
            buf.Add x
        return buf :> seq<_>
    }

    // "I will not use imperative code in F# again 😭"
    // |> Seq.replicate 100
    // |> Seq.iter (printfn"%s")
    let whileM f action =
        let buf = ResizeArray<_>()
        let rec impl() = sresult {
            let! x = f
            if x then
                let! y = action
                buf.Add y
                return! impl()
            else
                return buf :> seq<_>
        }
        impl()

    let whileFull f = whileM (get <!> (List.isEmpty >> not)) f

    open Aether

    let inline getOptic optic = sresult {
        let! x = get
        return Optic.get optic x
    }

    let inline setOptic optic value = sresult {
        let! x = get
        return! Optic.set optic value x |> put
    }

    let inline mapOptic optic f = sresult {
        let! x = get
        let s: 'c -> 'c = Optic.map optic f
        return! s x |> put
    }

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