// Learn more about F# at http://fsharp.org

open System
open System.Runtime.InteropServices
open BenchmarkDotNet
open BenchmarkDotNet.Running
open Farkle.Benchmarks

let isWindows64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture = Architecture.X64

let benchmarks = [|
    if isWindows64 then
        yield typeof<InceptionBenchmark>
    |]

[<EntryPoint>]
let main argv =
    benchmarks |> Array.iter (BenchmarkRunner.Run >> ignore)
    0
