// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.PrecompilerClient

open Serilog

let precompile (log: ILogger) references path =
    #if NETFRAMEWORK
    log.Error("Farkle can only precompile grammars on projects built with the .NET Core SDK (dotnet build etc). \
See more in https://teo-tsirpanis.github.io/Farkle/the-precompiler.html#Building-from-an-IDE")
    Ok []
    #else
    PrecompilerImplementation.discoverAndPrecompile log references path
    #endif
