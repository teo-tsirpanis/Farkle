// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Builder
open System
open System.Reflection

/// Loads the precompiled grammars from an assembly file.
type PrecompiledAssemblyFileLoader(path) =
    let resolver = PathAssemblyResolver([path; typeof<obj>.Assembly.Location])
    let loadContext = new MetadataLoadContext(resolver)
    let asm = loadContext.LoadFromAssemblyPath(path)
    let grammars = PrecompiledGrammar.GetAllFromAssembly asm

    member _.Grammars = grammars
    interface IDisposable with
        member _.Dispose() = loadContext.Dispose()
