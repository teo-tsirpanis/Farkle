// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.EGTReaderTests

open Expecto
open Farkle.Tests
open Farkle.Grammar.GOLDParser
open System
open System.IO

let testBinaryIO fWrite fRead message x =
    let stream = new MemoryStream()
    let br = new BinaryReader(stream)
    let bw = new BinaryWriter(stream)

    fWrite bw x
    bw.Flush()
    bw.Seek(0, SeekOrigin.Begin) |> ignore

    let xRead = fRead br

    Expect.equal xRead x message

[<Tests>]
let tests = testList "EGT Reader tests" [
    testProperty "Writing an EGT record and reading it back works" (
        testBinaryIO
            (fun w x -> EGTWriter.writeRecord w (ReadOnlySpan x))
            (fun r ->
                let mutable count = 0
                use mem = EGTReader.readRecord &count r
                mem.Memory.Slice(0, count).ToArray())
            "The EGT record that was read was not the same with what was written"
    )

    testProperty "Writing unsigned integers to an EGT file and reading them back works" (fun x ->
        let doTest =
            testBinaryIO
                (fun w -> Entry.UInt32 >> EGTWriter.writeEntry w)
                (fun r ->
                    match EGTReader.readEntry r with
                    | Entry.UInt32 x -> x
                    | _ -> failtest "Wrong entry type")
                "Got the wrong number while reading the EGT file back."
        
        // I have to make sure large numbers are tested as well.
        // The numbers FsCheck tests are pretty small.
        doTest <| x
        doTest <| x * 0x0101u
        doTest <| x * 0x010101u
        doTest <| x * 0x01010101u
    )
]
