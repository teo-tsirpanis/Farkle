// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EgtReader

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads
open System
open System.Text

type internal Entry =
    | Empty
    | Byte of byte
    | Boolean of bool
    | UInt16 of uint16
    | String of string

module internal LowLevel =

    open StateResult

    let takeByte = Seq.takeOne() |> mapFailure EGTReadError.SeqError <!> ((*) 1uy)

    let takeBytes count = count |> Seq.take |> mapFailure EGTReadError.SeqError <!> (Seq.map ((*) 1uy))

    let ensureLittleEndian x =
        if BitConverter.IsLittleEndian then
            x
        else
            ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

    let takeUInt16 = sresult {
        let! bytes = takeBytes 2 <!> Array.ofSeq
        return BitConverter.ToUInt16(bytes, 0) |> ensureLittleEndian
    }

    let takeString = sresult {
        let! len =
            get
            <!> Seq.pairs
            <!> Seq.tryFindIndex (fun (x, y) -> x = 0uy && y = 0uy)
            <!> failIfNone UnterminatedString
            <!> liftResult
            |> flatten
            <!> ((*) 2)
        let! result = takeBytes len <!> Array.ofSeq <!> Encoding.Unicode.GetString
        let! terminator = takeUInt16
        if terminator = 0us then
            return result
        else
            return! fail TakeStringBug
    }

    let readEntry = sresult {
        let! entryCode = takeByte <!> char
        match entryCode with
        | 'E' -> return Empty
        | 'b' -> return! takeByte <!> Byte
        | 'B' ->
            let! value = takeByte
            match value with
            | 0uy -> return Boolean false
            | 1uy -> return Boolean true
            | x -> return! x |> InvalidBoolValue |> fail
        | 'I' -> return! takeUInt16 <!> UInt16
        | 'S' -> return! takeString <!> String
        | x -> return! x |> InvalidEntryCode |> fail
    }


