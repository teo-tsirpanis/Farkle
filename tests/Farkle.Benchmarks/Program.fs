// Learn more about F# at http://fsharp.org

open System
open BenchmarkDotNet
open BenchmarkDotNet.Running
open Farkle.Benchmarks

[<EntryPoint>]
let main argv =
    BenchmarkSwitcher([| typeof<InceptionBenchmark> |]).Run(args = argv) |> ignore
    0
