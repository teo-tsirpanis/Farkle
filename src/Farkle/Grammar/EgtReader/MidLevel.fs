// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EgtReader

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads

type internal Record = Record of Entry list

type internal EGTFile = EGTFile of Record list

module internal MidLevel =

    open LowLevel
    open StateResult
    open System.IO
    open System.Text

    let readRecord = sresult {
        let! tag = takeByte <!> char
        if tag <> 'M' then
            do! tag |> InvalidRecordTag |> fail
        let! count = takeUInt16 <!> int
        return! count |> repeatM readEntry <!> List.ofSeq <!> Record
    }

    [<Literal>]
    let CGTHeader = "GOLD Parser Tables/1.0\0"
    [<Literal>]
    let EGTHeader = "GOLD Parser Tables/5.0\0"

    let readEGT = sresult {
        let! header =
            takeBytes (Encoding.Unicode.GetByteCount EGTHeader)
            <!> Array.ofSeq
            <!> Encoding.Unicode.GetString
        match header with
        | CGTHeader -> do! fail ReadACGTFile
        | EGTHeader -> do ()
        | _ -> do! fail UnknownFile
        return! whileM (Seq.isEmpty() |> liftState) readRecord <!> List.ofSeq <!> EGTFile
    }

    let eitherEntry fEmpty fByte fBoolean fUInt16 fString = sresult {
        let! entry = Seq.takeOne() |> mapFailure SeqError
        return!
            match entry with
            | Empty -> fEmpty ()
            | Byte x -> fByte x
            | Boolean x -> fBoolean x
            | UInt16 x -> fUInt16 x
            | String x -> fString x
    }

    let wantEmpty, wantByte, wantBoolean, wantUInt16, wantString =
        let fail x = fun entry -> x |> InvalidEntryType |> EGTReadError |> StateResult.fail
        let failEmpty x = fail "Empty" x
        let failByte x = fail "Byte" x
        let failBoolean x = fail "Boolean" x
        let failUInt16 x = fail "UInt16" x
        let failString x = fail "String" x
        let ok x = returnM x
        let wantEmpty  = eitherEntry ok failByte failBoolean failUInt16 failString
        let wantByte = eitherEntry failEmpty ok failBoolean failUInt16 failString
        let wantBoolean = eitherEntry failEmpty failByte ok failUInt16 failString
        let wantUInt16 = eitherEntry failEmpty failByte failBoolean ok failString
        let wantString = eitherEntry failEmpty failByte failBoolean failUInt16 ok
        wantEmpty, wantByte, wantBoolean, wantUInt16, wantString