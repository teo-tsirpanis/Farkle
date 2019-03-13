// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open Farkle.Monads.Either
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
                while r.BaseStream.Position < len && Option.isSome x do
                    x <- fRead r
                x
            with
            | _ -> None

        let readEntry r =
            match readByte r with
            | 'E'B -> Empty |> Some
            | 'b'B -> r |> readByte |> Byte |> Some
            | 'B'B -> r |> readByte |> ((<>) 0uy) |> Boolean |> Some
            | 'I'B -> r |> readUInt16 |> UInt16 |> Some
            | 'S'B -> r |> readNullTerminatedString |> String |> Some
            | _ -> None

        let readRecord fRecord r =
            match readByte r with
            | 'M'B ->
                let count = readUInt16 r |> int
                let arr = ArrayPool.Shared.Rent count
                use _release = {new System.IDisposable with member __.Dispose() = ArrayPool.Shared.Return arr}
                let mutable i = 0
                let mutable x = Some Unchecked.defaultof<_>
                while i < count && x.IsSome do
                    x <- readEntry r
                    Option.iter (Array.set arr i) x
                    i <- i + 1
                x |> Option.bind (fun _ -> System.ReadOnlyMemory(arr, 0, count) |> fRecord)
            | _ -> None

        let readRecords fRead r =
            readToEnd (readRecord fRead) r

    open Implementation

    let readEGT defaultRecordError fHeaderCheck fRecord r = either {
        do! readNullTerminatedString r |> fHeaderCheck
        do! readRecords fRecord r |> failIfNone defaultRecordError
    }
