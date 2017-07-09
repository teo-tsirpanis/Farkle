// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Chessie.ErrorHandling
open Monads.StateResult

type SeqError = EOF | InvalidLength

module Seq =

    let rec pairs x = seq {
        if not <| Seq.isEmpty x then
            let first, rest = Seq.head x, Seq.tail x
            if not <| Seq.isEmpty rest then
                let second, rest = Seq.head rest, Seq.tail rest
                yield first, second
                yield! pairs rest}

    let takeOne() = sresult {
        let! s = get
        do! s |> Seq.tail |> put
        return! s |> Seq.tryHead |> failIfNone EOF |> liftResult
    }

    let peekOne() = get >>= (Seq.tryHead >> failIfNone EOF >> liftResult)

    let peek count = sresult {
        let! s = get
        if Seq.length s < count then
            return! fail InvalidLength
        else
            return s |> Seq.take count
    }

    let peekNext count = sresult {
        let! s = get
        if Seq.length s < count then
            return! fail InvalidLength
        else
            return s |> Seq.skip count
    }

    let take count = sresult {
        let! s = get
        let! result = peek count
        do! peekNext count >>= put
        return result
    }

    let takeWhile f = sresult {
        let! s = get
        let result = s |> Seq.takeWhile f
        do! s |> Seq.skipWhile f |> put
        return result
    }

    let skip count = sresult {
        do! peekNext count >>= put
        return! get
    }

    let skipWhile f = sresult {
        do! takeWhile f |> ignore
        return! get
    }
