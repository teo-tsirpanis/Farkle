// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle

/// What can go wrong with reading an EGT file.
type EGTReadError =
    /// An invalid entry code was encountered.
    /// Valid entry codes are these letters: `EbBIS`.
    | InvalidEntryCode of byte
    /// An entry of `expected` type was requested, but something else was returned instead.
    | InvalidEntryType of expected: string
    /// The string you asked for is not terminated
    | UnterminatedString
    /// takeString has a bug. The developer _should_ be contacted
    /// in case this type of error is encountered
    | TakeStringBug
    /// Records should start with `M`, but this one started with something else.
    | InvalidRecordTag of byte
    /// The file's structure is not recognized. This is a generic error.
    | UnknownEGTFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile
    /// The item at the given index of a list was not found.
    | IndexNotFound of uint32
    /// Unexpected end of file.
    | UnexpectedEOF
    with
        override x.ToString() =
            match x with
            | InvalidEntryCode x -> x |> char |> sprintf "Invalid entry code: '%c'."
            | InvalidEntryType x -> sprintf "Unexpected entry type. Expected a %s." x
            | UnterminatedString -> "String terminator was not found."
            | TakeStringBug -> "The function takeString exhibited a very unlikely bug. If you see this error, please file an issue on GitHub."
            | InvalidRecordTag x -> x |> char |> sprintf "The record tag '%c' is not 'M', as it should have been."
            | UnknownEGTFile -> "The given grammar file is not recognized."
            | ReadACGTFile ->
                "The given file is a CGT file, not an EGT one."
                + " You should update to the latest version of GOLD Parser Builder (at least over Version 5.0.0)"
                + " and save the tables as \"Enhanced Grammar tables (Version 5.0)\"."
            | IndexNotFound x -> sprintf "The index %d was not found in a list." x
            | UnexpectedEOF -> "Unexpected end of file."

/// An entry of an EGT file.
type Entry =
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

/// An EGT record is a list of grouped entries.
type Record = Entry list

/// An EGT file is made of a header string and a list of `Record`s.
type EGTFile = {
    /// [omit]
    Header: string
    /// [omit]
    Records: Record list
}