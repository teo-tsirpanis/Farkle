// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Loggers
open Farkle
open Farkle.Grammar.GOLDParser
open System
open System.IO
open System.Runtime.Serialization.Formatters.Binary

type SerializationBenchmark() =

    let logger = BenchmarkDotNet.Loggers.ConsoleLogger() :> ILogger

    let mutable base64EGT = ""

    let mutable serialized = ""

    let serializeIt (x: GOLDGrammar) =
        let f = BinaryFormatter()
        use memStream = new MemoryStream()
        f.Serialize(memStream, x)
        memStream.ToArray() |> Convert.ToBase64String

    let deserializeIt x =
        let memStream = new MemoryStream(Convert.FromBase64String x)
        let f = BinaryFormatter()
        f.Deserialize memStream :?> GOLDGrammar

    [<GlobalSetup>]
    member __.Setup() =
        let bytes = File.ReadAllBytes "inception.egt"
        base64EGT <- Convert.ToBase64String bytes
        logger.WriteLineInfo <| sprintf "EGT as Base-64: %d characters" base64EGT.Length
        use stream = new MemoryStream(bytes)
        serialized <- stream |> EGT.ofStream |> returnOrFail |> serializeIt
        logger.WriteLineInfo <| sprintf "Serialized grammar as Base64: %d characters" serialized.Length

    [<Benchmark>]
    /// The most straightforward option, encoding the EGT file in Base64.
    /// ## Pros:
    /// * Works outside .NET
    /// * Version-tolerant
    /// * Faster
    /// * Space-efficient
    /// ## Cons:
    /// * Uses more memory and causes more GC collections (not actually a problem; it is supposed to be called only once)
    member __.Base64EGT() =
        use stream = new MemoryStream(Convert.FromBase64String base64EGT)
        stream |> EGT.ofStream |> returnOrFail

    [<Benchmark>]
    /// Another option, directly serializing the GOLDGrammar object using the binary serializer, with a crude version validator.
    /// ## Pros:
    /// * Uses less memory and causes less GC collections
    /// ## Cons
    /// * Works only inside .NET
    /// * Not version-tolerant (it isn't supposed to be)
    /// * Slower
    /// * Cannot be optimized because it is a .NET internal process
    /// * Less space-efficient
    member __.Serialized() = serialized |> deserializeIt
    