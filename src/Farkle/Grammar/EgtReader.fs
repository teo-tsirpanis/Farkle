// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Chessie.ErrorHandling
open Farkle
open Monads.StateResult
open System

type RecordType =
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

type Entry =
    | Empty
    | Byte of byte
    | Boolean of bool
    | UInt16 of uint16
    | String of string

/// What can go wrong with reading an EGT file.
type EGTReadError =
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
    | InvalidEntryType of expected: string * found: Entry
    /// The string you asked for is not terminated
    | UnterminatedString
    /// takeString has a bug. The developer _should_ be contacted
    /// in case this type of error is encountered
    | TakeStringBug
    /// Records should start with `M`, but this one started with something else.
    | InvalidRecordTag of char
    /// The file's header is invalid.
    | UnknownFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile

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

    let takeString = sresult {
        let! len =
            get
            <!> Seq.pairs
            <!> Seq.tryFindIndex (fun (x, y) -> x = 0uy && y = 0uy)
            <!> failIfNone UnterminatedString
            <!> liftResult
            |> flatten
            <!> ((*) 2)
        let! result = takeChars len <!> Array.ofSeq <!> System.String
        let! terminator = takeUInt16
        if terminator = 0us then
            return result
        else
            return! fail TakeStringBug
    }

    let readEntry = sresult {
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

type Record = Record of Entry list

module HighLevel =

    open EgtReader

    let readRecord = sresult {
        let! tag = takeChar
        if tag <> 'M' then
            do! tag |> InvalidRecordTag |> fail
        let! count = takeUInt16 <!> int
        return! count |> repeatM readEntry <!> List.ofSeq <!> Record
    }

    [<Literal>]
    let CGTHeader = "GOLD Parser Tables/1.0"
    [<Literal>]
    let EGTHeader = "GOLD Parser Tables/5.0"

    let readEGT = sresult {
        let! header = takeChars EGTHeader.Length <!> Array.ofSeq <!> System.String
        match header with
        | CGTHeader -> do! fail ReadACGTFile
        | EGTHeader -> do ()
        | _ -> do! fail UnknownFile
    }
