// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open FSharpx.Collections
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Attributes

type RandomAccessBenchmark() =

    let mutable idx = 0

    let mutable arr = [| |]
    let mutable ral = RandomAccessList.empty
    let mutable map = Map.empty
    
    [<Params (10,100,1000,10000)>] 
    member val public Length = 0 with get, set

    [<GlobalSetup>]
    member x.Setup() =
        idx <- x.Length * 75 / 100
        arr <- Array.replicate x.Length 0
        ral <- RandomAccessList.init x.Length (fun _ -> 0)
        map <- Seq.init x.Length id |> Seq.map (fun x -> x, 0) |> Map.ofSeq

    [<Benchmark>]
    member __.ArrayBenchmark() = arr.[idx]
    [<Benchmark>]
    member __.RAListBenchmark() = ral.[idx]
    [<Benchmark>]
    member __.MapBenchmark() = map.[idx]

