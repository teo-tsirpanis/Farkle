// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open System.IO
open System.Text

module internal EGTReader =

    module private Implementation =

        let inline eofGuard fRead (r: BinaryReader) =
            try
                if r.BaseStream.Position <> r.BaseStream.Length then
                    fRead r |> Some
                else
                    None
            with
            | :? EndOfStreamException -> None

        let inline readByte (r: BinaryReader) = r.ReadByte()

        let inline readUInt16 (r: BinaryReader) =
            let x = r.ReadUInt16()
            if System.BitConverter.IsLittleEndian then
                x
            else
                ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

        let readNullTerminatedString r =
            let sr = StringBuilder()
            let mutable c = readUInt16 r
            while c <> 0us do
                c |> char |> sr.Append |> ignore
                c <- readUInt16 r
            sr.ToString()

        let readToEnd fRead r = Seq.unfold (fun r -> eofGuard fRead r |> Option.map (fun x -> x, r)) r

        let readEntry r =
            match readByte r with
            | 'E'B -> Empty |> Ok
            | 'b'B -> r |> readByte |> Byte |> Ok
            | 'B'B -> r |> readByte |> ((<>) 0uy) |> Boolean |> Ok
            | 'I'B -> r |> readUInt16 |> UInt16 |> Ok
            | 'S'B -> r |> readNullTerminatedString |> String |> Ok
            | b -> b |> InvalidEntryCode |> Error

        let readRecord r: Result<Record,_> =
            match readByte r with
            | 'M'B ->
                let count = readUInt16 r
                Seq.init (int count) (fun _ -> readEntry r) |> collect
            | b -> b |> InvalidRecordTag |> Error

        let readRecords r =
            readToEnd readRecord r


    open Implementation

    let readEGT r =
        let header = readNullTerminatedString r
        let records = readRecords r
        records |> collect |> Result.map (fun records -> {Header = header; Records = records})

    let readEGT2 fHeaderCheck fFirst fRest r = either {
        let header = readNullTerminatedString r
        do! fHeaderCheck header
        let! first = readRecord r
        do! first |> fFirst
        for x in readRecords r do
            do! x >>= fRest
    }
