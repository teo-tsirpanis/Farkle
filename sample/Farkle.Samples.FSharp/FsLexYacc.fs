// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module FsLexYacc.JSON.JSONParser

open FSharp.Text.Lexing
open System.IO

let parseString x =
    let lexBuf = LexBuffer<_>.FromString x
    Parser.value Lexer.read lexBuf

let parseTextReader tr =
    // Holy fuzzy, they didn't even
    // make LexBuffers disposable!
    let lexBuf = LexBuffer<_>.FromTextReader tr
    Parser.value Lexer.read lexBuf

let parseFile path =
    use f = File.OpenText path
    parseTextReader f
