// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

/// What can go wrong with reading an EGT file.
[<Struct>]
type EGTReadError =
    /// The Base64-encoded string of the EGT file is invalid.
    | InvalidBase64Format
    /// The grammar file is not recognized.
    | InvalidEGTFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile
    /// A production of the grammar contains a
    /// group end symbol as one of its members.
    | ProductionHasGroupEnd of index: uint32
    with
        override x.ToString() =
            match x with
            | InvalidBase64Format -> "The Base64-encoded string of the EGT file is invalid."
            | InvalidEGTFile -> "The given grammar file is not recognized."
            | ReadACGTFile -> "The given file is a CGT file, not an EGT one. \
You should update to the latest version of GOLD Parser Builder (at least over Version 5.0.0) \
and save the tables as \"Enhanced Grammar tables (Version 5.0)\"."
            | ProductionHasGroupEnd index -> sprintf "Production #%d has a group end symbol as one of its members. \
This is allowed in GOLD Parser, but not supported in Farkle." index

exception internal EGTFileException

exception internal ProductionHasGroupEndException of index: uint32

/// An entry of an EGT file.
[<Struct>]
type internal Entry =
    /// [omit]
    | Empty
    /// [omit]
    | Byte of byteValue: byte
    /// [omit]
    | Boolean of boolValue: bool
    /// [omit]
    | UInt16 of intValue: uint16
    /// [omit]
    | String of stringValue: string
