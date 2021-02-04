// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Builder
open Farkle.Grammar

type AssemblyReference = {
    AssemblyFullName: string
    FileName: string
}

type PrecompilerResult =
    | Successful of Grammar
    | PrecompilingFailed of grammarName: string * BuildError list
    | DiscoveringFailed of typeName: string * fieldName: string * exn
