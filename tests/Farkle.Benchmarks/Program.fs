// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open BenchmarkDotNet.Running
open System.Reflection

[<EntryPoint>]
let main argv =
    BenchmarkSwitcher.FromAssembly(Assembly.GetEntryAssembly()).Run(argv) |> ignore
    0
