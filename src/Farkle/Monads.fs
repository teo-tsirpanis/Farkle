// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Monads

/// [omit]
module internal Either =

    let rec doWhile fGuard fBody =
        if fGuard() then
            match fBody() with
            | Ok () -> doWhile fGuard fBody
            | Error _ as x -> x
        else
            Ok ()

    let doFor (xs: _ seq) fBody =
        use enumerator = xs.GetEnumerator()
        let rec impl() =
            if enumerator.MoveNext() then
                match fBody enumerator.Current with
                | Ok () -> impl()
                | Error _ as x -> x
            else
                Ok ()
        impl()

    type EitherBuilder() =
        member _.Zero() = Ok ()
        member _.Bind(m, f) = Result.bind f m
        member _.Return(x) = Ok x
        member _.ReturnFrom(x) = x
        member _.Combine (a, b) = Result.bind b a
        member _.Delay f = f
        member _.Run f = f ()
        member _.TryWith (body, handler) =
            try
                body()
            with
            | e -> handler e
        member _.TryFinally (body, compensation) =
            try
                body()
            finally
                compensation()
        member _.Using(d, body) =
            use d = d
            body d
        member x.While(guard, body) = doWhile guard body
        member x.For(s:seq<_>, body) = doFor s body

    /// Wraps computations in an error handling computation expression.
    let either = EitherBuilder()
