// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open System.Buffers
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
                // No need to get the length each time; it stays the same, and it's quite expensive, as it does some Win32 calls.
                // The stream's position on the other hand is very fast. It is just a private variable read.
                let len = r.BaseStream.Length
                let mutable x = fRead r
                while r.BaseStream.Position < len && isOk x do
                    x <- fRead r
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

        let readRecord fRecord r =
            match readByte r with
            | 'M'B ->
                let count = readUInt16 r |> int
                let arr = ArrayPool.Shared.Rent count
                use _release = {new System.IDisposable with member __.Dispose() = ArrayPool.Shared.Return arr}
                let mutable i = 0
                let mutable x = Ok Unchecked.defaultof<_>
                while i < count && isOk x do
                    x <- readEntry r
                    tee (Array.set arr i) ignore x
                    i <- i + 1
                x |> Result.bind (fun _ -> System.ReadOnlyMemory(arr, 0, count) |> fRecord)
            | b -> b |> InvalidRecordTag |> Error

        let readRecords fRead r =
            readToEnd (readRecord fRead) r


    open Implementation

    let readEGT fHeaderCheck fRecord r = either {
        do! readNullTerminatedString r |> fHeaderCheck
        do! readRecords fRecord r
    }
