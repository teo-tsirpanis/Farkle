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

        let readToEnd fRead (r: BinaryReader) =
            try
                let mutable x = fRead r
                while r.BaseStream.Position < r.BaseStream.Length && not <| isError x do
                    x <- fRead r
                if r.BaseStream.Position >= r.BaseStream.Length then
                    Ok ()
                else
                    x
            with
            | :? EndOfStreamException -> Error UnexpectedEOF

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

        let readRecords fRead r =
            let fRead x = x |> readRecord |> Result.bind fRead
            readToEnd fRead r


    open Implementation

    let readEGT fHeaderCheck fRecord r = either {
        do! readNullTerminatedString r |> fHeaderCheck
        do! readRecords fRecord r
    }
