// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tools.Common

open System.Reflection

let toolsVersion =
    let asm = Assembly.GetExecutingAssembly()
    asm.GetCustomAttributes<AssemblyInformationalVersionAttribute>()
    |> Seq.map(fun x -> x.InformationalVersion)
    |> Seq.tryExactlyOne
    |> Option.defaultWith (fun () -> asm.GetName().Version.ToString())
