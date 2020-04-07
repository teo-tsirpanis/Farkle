// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Running
open System.Reflection

[<EntryPoint>]
let main argv =
    BenchmarkSwitcher
        .FromAssembly(Assembly.GetEntryAssembly())
        // FParsec is shipped in Debug mode. See their issue 44.
        .Run(argv, ManualConfig.Create(DefaultConfig.Instance).WithOptions(ConfigOptions.DisableOptimizationsValidator))
    |> ignore
    0
