// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.EgtReader

open Chessie.ErrorHandling
open Farkle
open Monads.StateResult
open System

type EGTRecord =
    | Charset = 67uy
    | DFAState = 68uy
    | InitialStates = 73uy
    | LRState = 76uy
    | Parameter = 80uy
    | Rule = 82uy
    | Symbol = 83uy
    | CharacterRanges = 99uy
    | Group = 103uy
    | GroupNesting = 110uy
    | Property = 112uy
    | Counts = 116uy

type EGTEntry =
    | Empty
    | Byte of byte
    | Boolean of bool
    | UInt16 of uint16
    | String of string

type EGTError =
    | SeqError of SeqError
    | InvalidBoolValue of byte
    | InvalidEntryCode of char

module EgtReader =
    let byteToChar = (*) 1uy >> char
    let takeChar = Seq.takeOne() |> mapFailure SeqError <!> byteToChar
    let takeChars count = count |> Seq.take |> mapFailure SeqError <!> (Seq.map byteToChar)
    let takeByte = takeChar <!> byte
    let takeBytes count = count |> takeChars <!> (Seq.map byte)
    let ensureLittleEndian x =
        if BitConverter.IsLittleEndian then
            x
        else
            ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)
    let takeUInt16 = sresult {
        let! bytes = takeBytes 2 <!> Array.ofSeq
        return BitConverter.ToUInt16(bytes, 0) |> ensureLittleEndian
    }
    let takeString =
            let rec takeString s = sresult {
                let! num = takeUInt16
                if num <> 0us then
                    let c = char num
                    return! takeString (sprintf "%s%c" s c)
                else
                    return s
            }
            takeString ""
    let readEntry() = sresult {
        let! entryCode = takeChar
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
