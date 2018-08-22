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

        let eofGuard fRead r =
            try
                fRead r |> Some
            with
            | :? EndOfStreamException -> None

        let readByte (r: BinaryReader) = r.ReadByte()

        let readUInt16 (r: BinaryReader) =
            let x = r.ReadUInt16()
            if System.BitConverter.IsLittleEndian then
                x
            else
                ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

        let readNullTerminatedString r =
            let sr = StringBuilder()
            let mutable c = readUInt16 r
            while c <> 0us do
                c |> char |> sr.Append |> Operators.ignore
                c <- readUInt16 r
            sr.ToString()

        let readToEnd fRead r = Seq.unfold (fun r -> eofGuard fRead r |> Option.map (fun x -> x, r)) r |> collect

        let readEntry r =
            match readByte r with
            | 'E'B -> Empty |> Ok
            | 'b'B -> r |> readByte |> Byte |> Ok
            | 'B'B -> r |> readByte |> ((<>) 0uy) |> Boolean |> Ok
            | 'I'B -> r |> readUInt16 |> UInt16 |> Ok
            | 'S'B -> r |> readNullTerminatedString |> String |> Ok
            | b -> b |> InvalidEntryCode |> Error

        let readRecord r =
            match readByte r with
            | 'M'B ->
                let count = readUInt16 r
                Seq.init (int count) (fun _ -> readEntry r) |> collect |> Result.map Record
            | b -> b |> InvalidRecordTag |> Error
        
        let readRecords r =
            readToEnd readRecord r


    open Implementation

    let readEGT r =
        let header = readNullTerminatedString r
        let records = readRecords r
        records |> Result.map (fun records -> {Header = header; Records = records})
