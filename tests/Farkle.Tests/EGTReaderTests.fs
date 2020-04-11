// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.EGTReaderTests

open Expecto
open Farkle.Tests
open Farkle.Grammar
open Farkle.Grammar.EGTFile
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

let egtNeoTests =
    allEGTFiles
    |> List.map (fun egtFile ->
        let testName = sprintf "Roundtripping %s into the EGTneo format works" <| Path.GetFileName egtFile
        test testName {
            let grammar = EGT.ofFile egtFile
            use s1 = new MemoryStream()
            EGT.toStreamNeo s1 grammar
            s1.Position <- 0L
            let grammar2 = EGT.ofStream s1
            use s2 = new MemoryStream()
            EGT.toStreamNeo s2 grammar2

            // Grammars follow reference equality semantics,
            // so we will check the streams for equality.
            s1.Position <- 0L
            s2.Position <- 0L
            Expect.streamsEqual s1 s2 ""
        })

[<Tests>]
let tests = testList "EGT Reader tests" [
    testProperty "Writing an EGT record and reading it back works" (
        testBinaryIO
            (fun w x -> EGTWriter.writeRecord w (ReadOnlySpan x))
            (fun r ->
                // An empty array will force the
                // reader to make it exactly fit.
                let mutable arr = [||]
                EGTReader.readRecord &arr r |> ignore
                arr)
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
                "Got the wrong number while reading the EGT file back"

        // I have to make sure large numbers are tested as well.
        // The numbers FsCheck tests are pretty small.
        doTest <| x
        doTest <| x * 0x0101u
        doTest <| x * 0x010101u
        doTest <| x * 0x01010101u
    )

    yield! egtNeoTests

    test "The EGTneo file format is stable" {
        // This test was made just to ensure the EGTneo file format
        // does change in a breaking way without us knowing.
        "JSON.egtn" |> getResourceFile |> EGT.ofFile |> ignore
    }
]
