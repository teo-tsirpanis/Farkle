// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

module Farkle.Benchmarks.FsLexYacc.Json

open FSharp.Text.Lexing
open Farkle.Benchmarks.FsLexYacc.JsonImpl

[<CompiledName("ParseString")>]
let parseString x =
    let lexBuf = LexBuffer<_>.FromString x
    Parser.value Lexer.read lexBuf
