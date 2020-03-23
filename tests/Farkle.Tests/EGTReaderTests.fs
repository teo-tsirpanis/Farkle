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

[<Tests>]
let tests = testList "EGT Reader tests" [
    testProperty "Writing an EGT record and reading it back works" (fun (record: _ []) ->
        let stream = new MemoryStream()
        let br = new BinaryReader(stream)
        let bw = new BinaryWriter(stream)

        EGTWriter.writeRecord bw (ReadOnlySpan record)
        bw.Flush()
        bw.Seek(0, SeekOrigin.Begin) |> ignore

        let mutable entryCount = 0
        use mem = EGTReader.readRecord &entryCount br
        let readRecord = mem.Memory.Slice(0, entryCount).ToArray()

        Expect.sequenceEqual readRecord record "The EGT record that was read was not the same with what was written")
]