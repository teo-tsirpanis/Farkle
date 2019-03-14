// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

/// What can go wrong with reading an EGT file.
[<Struct>]
type EGTReadError =
    /// The grammar file is not recognized.
    | InvalidEGTFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile
    with
        override x.ToString() =
            match x with
            | InvalidEGTFile -> "The given grammar file is not recognized."
            | ReadACGTFile ->
                "The given file is a CGT file, not an EGT one."
                + " You should update to the latest version of GOLD Parser Builder (at least over Version 5.0.0)"
                + " and save the tables as \"Enhanced Grammar tables (Version 5.0)\"."

/// An entry of an EGT file.
type internal Entry =
    /// [omit]
    | Empty
    /// [omit]
    | Byte of byte
    /// [omit]
    | Boolean of bool
    /// [omit]
    | UInt16 of uint16
    /// [omit]
    | String of string
