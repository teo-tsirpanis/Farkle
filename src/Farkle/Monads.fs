// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// [omit]
// I cannot make it internal, because it is used by Farkle.Tools and Farkle.Tools.MSBuild.
module Farkle.Monads

open System
open System.Diagnostics

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
                if not <| isNull d then
                    d.Dispose())
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

/// [omit]
module Either =

    type EitherBuilder() =
        member __.Zero() = Ok ()
        member __.Bind(m, f) = Result.bind f m
        member __.Return(x) = Ok x
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = Result.bind b a
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
                if not <| isNull d then
                    d.Dispose())
        member x.While (guard, body) =
            if not <| guard () then
                x.Zero()
            else
                Result.bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    /// Wraps computations in an error handling computation expression.
    let either = EitherBuilder()
