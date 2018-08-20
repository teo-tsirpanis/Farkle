// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Benchmarks

open BenchmarkDotNet.Attributes
open FSharpx.Collections
open System.Collections.Immutable

/// This benchmark measures which data structure is faster for indexed access.
type RandomAccessBenchmark() =

    let mutable idx = 0

    let mutable arr = [| |]
    let mutable ral = RandomAccessList.empty
    let mutable map = Map.empty
    let mutable immArr = ImmutableArray.Empty
    let mutable immDict = ImmutableDictionary.Empty
    let mutable immSDict = ImmutableSortedDictionary.Empty

    [<Params (10,100,1000,10000)>]
    member val public Length = 0 with get, set

    [<GlobalSetup>]
    member x.Setup() =
        idx <- x.Length * 75 / 100
        arr <- Array.replicate x.Length 0
        ral <- RandomAccessList.init x.Length (fun _ -> 0)
        map <- Seq.init x.Length id |> Seq.map (fun x -> x, 0) |> Map.ofSeq
        immArr <- arr.ToImmutableArray()
        immDict <- map.ToImmutableDictionary()
        immSDict <- map.ToImmutableSortedDictionary()

    [<Benchmark>]
    member __.Array() = arr.[idx]
    [<Benchmark>]
    member __.RAList() = ral.[idx]
    [<Benchmark>]
    member __.Map() = map.[idx]
    [<Benchmark>]
    member __.ImmutableArray() = immArr.[idx]
    [<Benchmark>]
    member __.ImmutableDictionary() = immDict.[idx]
    [<Benchmark>]
    member __.ImmutableSortedDictionary() = immSDict.[idx]

