// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Chessie.ErrorHandling
open Farkle
open Farkle.Monads
open System
open System.Text

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
    /// The file you specified does not exist.
    | FileNotExist of string

module private EgtReaderImpl =

    open StateResult

    let takeByte = Seq.takeOne() |> mapFailure SeqError <!> ((*) 1uy)

    let takeBytes count = count |> Seq.take |> mapFailure SeqError <!> (Seq.map ((*) 1uy))

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

type Record = Record of Entry list

type EGTFile = EGTFile of Record list

module EgtReader =

    open EgtReaderImpl
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

    let readEGTFromFile path = trial {
        if path |> File.Exists |> not then
            do! path |> FileNotExist |> Trial.fail
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        return! stream |> Seq.ofByteStream |> eval readEGT
    }

    let readEGTFromBytes = eval readEGT
