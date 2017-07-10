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

/// What can go wrong with reading an EGT file.
type EGTError =
    /// A [sequence error](Farkle.SeqError) did happen.
    | SeqError of SeqError
    /// Boolean values should only be `0` or `1`.
    /// If they are not, thet it's undefined by the documentation.
    /// But we will call it an error.
    | InvalidBoolValue of byte
    /// An invalid entry code was encountered.
    /// Valid entry codes are these letters: `EbBIS`.
    | InvalidEntryCode of char
    /// An entry of `expected` type was requested, but `found` was returned instead.
    | InvalidEntryType of expected: string * found: EGTEntry

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
    
    let eitherEntry fEmpty fByte fBoolean fUInt16 fString entry =
        match entry with
        | Empty -> fEmpty entry ()
        | Byte x -> fByte entry x
        | Boolean x -> fBoolean entry x
        | UInt16 x -> fUInt16 entry x
        | String x -> fString entry x

    let wantEmpty, wantByte, wantBoolean, wantUInt16, wantString =
        let fail x = fun entry _ -> (x, entry) |> InvalidEntryType |> Trial.fail
        let failEmpty x = fail "Empty" x
        let failByte x = fail "Byte" x
        let failBoolean x = fail "Boolean" x
        let failUInt16 x = fail "UInt16" x
        let failString x = fail "String" x
        let ok _ x = ok x
        let wantEmpty x  = eitherEntry ok failByte failBoolean failUInt16 failString x
        let wantByte x  = eitherEntry failEmpty ok failBoolean failUInt16 failString x
        let wantBoolean x = eitherEntry failEmpty failByte ok failUInt16 failString x
        let wantUInt16 x = eitherEntry failEmpty failByte failBoolean ok failString x
        let wantString x = eitherEntry failEmpty failByte failBoolean failUInt16 ok x
        wantEmpty, wantByte, wantBoolean, wantUInt16, wantString
