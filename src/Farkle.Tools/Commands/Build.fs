// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.Build

open Argu
open Farkle.Monads.Either
open Farkle.Tools
open Farkle.Tools.Precompiler
open Serilog
open System.IO

type Arguments =
    | [<ExactlyOnce; MainCommand>] InputAssembly of string
    | [<Unique; AltCommandLine("-o")>] OutputAssembly of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | InputAssembly _ -> "The assembly file to process."
            | OutputAssembly _ -> "The path to write the processed assembly. Defaults to the input assembly."

let run (args: ParseResults<_>) = either {
    let! inputAssembly = args.PostProcessResult(InputAssembly, assertFileExists)
    let outputAssembly = args.GetResult(OutputAssembly, inputAssembly)

    do! precompile Log.Logger inputAssembly outputAssembly
}
