// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open BenchmarkDotNet.Running
open Farkle.Benchmarks
open System.Reflection

let benchmarks = [|
    yield typeof<InceptionBenchmark>
    yield typeof<RandomAccessBenchmark>
    |]

[<EntryPoint>]
let main argv =
    BenchmarkSwitcher.FromAssembly(Assembly.GetEntryAssembly()).Run(argv) |> ignore
    0
