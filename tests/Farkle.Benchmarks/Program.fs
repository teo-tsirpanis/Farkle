// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open BenchmarkDotNet.Running
open Farkle.Benchmarks

let benchmarks = [|
    yield typeof<InceptionBenchmark>
    yield typeof<RandomAccessBenchmark>
    |]

[<EntryPoint>]
let main argv =
    benchmarks |> Array.iter (BenchmarkRunner.Run >> ignore)
    0
