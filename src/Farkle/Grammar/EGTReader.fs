// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle.Monads.Either
open System
open System.IO
open System.Text

module internal EGTReader =

    let invalidEGT() = raise EGTFileException

    module private Implementation =

        let inline readByte (r: BinaryReader) = r.ReadByte()

        let inline readUInt16 (r: BinaryReader) =
            let x = r.ReadUInt16()
            if BitConverter.IsLittleEndian then
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

        let readEntry r =
            match readByte r with
            | 'E'B -> Empty
            | 'b'B -> r |> readByte |> Entry.Byte
            | 'B'B -> r |> readByte |> ((<>) 0uy) |> Entry.Boolean
            | 'I'B -> r |> readUInt16 |> Entry.UInt16
            | 'S'B -> r |> readNullTerminatedString |> Entry.String
            | _ -> invalidEGT()

        let readRecords fRead (r: BinaryReader) =
            try
                // No need to get the length each time; it stays the same, and it's quite expensive, as it does some Win32 calls.
                // The stream's position on the other hand is very fast. It is just a private variable read.
                let len = r.BaseStream.Length
                let mutable arr = Array.zeroCreate 45
                while r.BaseStream.Position < len do
                    match readByte r with
                    | 'M'B ->
                        let count = readUInt16 r |> int
                        while count > arr.Length do
                            Array.Resize(&arr, arr.Length * 2)
                        for i = 0 to count - 1 do
                            arr.[i] <- readEntry r
                        ReadOnlyMemory(arr, 0, count) |> fRead
                    | _ -> invalidEGT()
                Ok ()
            with
            | :? EndOfStreamException | EGTFileException -> Error InvalidEGTFile
            | ProductionHasGroupEndException index -> Error <| ProductionHasGroupEnd index

    open Implementation

    let readEGT fHeaderCheck fRecord r = either {
        do! readNullTerminatedString r |> fHeaderCheck
        do! readRecords fRecord r
    }

    let lengthMustBe (m: ReadOnlyMemory<_>) expectedLength =
        if m.Length <> expectedLength then
            invalidEGT()

    let lengthMustBeAtLeast (m: ReadOnlyMemory<_>) expectedLength =
        if m.Length < expectedLength then
            invalidEGT()

    // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
    // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
    // FFFFFFfFFFFFFF
    let wantEmpty (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Empty -> () | _ -> invalidEGT()
    let wantByte (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Byte x -> x | _ -> invalidEGT()
    let wantBoolean (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | Boolean x -> x | _ -> invalidEGT()
    let wantUInt16 (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | UInt16 x -> x | _ -> invalidEGT()
    let wantString (x: ReadOnlyMemory<_>) idx = match x.Span.[idx] with | String x -> x | _ -> invalidEGT()
