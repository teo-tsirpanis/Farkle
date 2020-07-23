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

let private testBinaryIO fWrite fRead message x =
    let stream = new MemoryStream()

    do
        use ew = new EGTWriter(stream, "Farkle Test EGT File", true)
        fWrite ew x
    stream.Position <- 0L

    let xRead =
        use er = new EGTReader(stream, true)
        fRead er

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
            (fun w x -> w.WriteFullRecord(ReadOnlySpan x))
            (fun er ->
                er.NextRecord()
                er.Span.ToArray())
            "The EGT record that was read was not the same with what was written"
    )

    testProperty "Writing unsigned integers to an EGT file and reading them back works" (fun x ->
        let doTest x =
            testBinaryIO
                (fun w x ->
                    let entry = Entry.UInt32 x
                    w.WriteEntry entry
                    w.FinishPendingRecord())
                (fun er ->
                    er.NextRecord()
                    wantUInt32 er.Span 0)
                "Got the wrong number while reading the EGT file back" x

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
        // does not change in a breaking way without us knowing.
        "JSON.egtn" |> getResourceFile |> EGT.ofFile |> ignore
    }
]
