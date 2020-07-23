// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Grammar.EGTFile
open EGTHeaders
open EGTWriter
open System
open System.IO
open System.Text

/// Functions to read and write grammars from EGT files.
/// Grammars can be read either from GOLD Parser's Enhanced
/// Grammar Tables (version 5.0) or from Farkle's EGTneo format.
/// EGTneo (new encoding option) is a file format designed for Farkle that
/// is more compact and easier to read. GOLD Parser cannot read EGTneo files.
/// Grammars can only be written in the EGTneo format.
module EGT =

    /// Reads a `Grammar` from a stream.
    [<CompiledName("ReadFromStream")>]
    let ofStream stream =
        use er = new EGTReader(stream, true)
        match er.Header with
        | CGTHeader -> invalidEGTf "This file is a legacy GOLD Parser 1.0 file, \
which is not supported. You should update to the last version of GOLD Parser and save \
it as an \"Enhanced Grammar Tables (version 5.0)\"."
        | EGTHeader -> EGTLegacyReader.read er
        | EGTNeoHeader -> EGTNeoReader.read er
        | _ -> invalidEGT()

    /// Reads a `Grammar` from a Base64-encoded string.
    [<CompiledName("ReadFromBase64String")>]
    let ofBase64String str =
        let x = Convert.FromBase64String str
        use s = new MemoryStream(x, false)
        ofStream s

    /// Reads an EGT file from the file system and returns a `Grammar`.
    [<CompiledName("ReadFromFile")>]
    let ofFile path =
        use stream = File.OpenRead path
        ofStream stream

    /// Writes the given `Grammar` to a stream in the EGTneo format.
    [<CompiledName("WriteToStreamNeo")>]
    let toStreamNeo stream grammar =
        use w = new BinaryWriter(stream, Encoding.UTF8, true)
        writeNullTerminatedString EGTNeoHeader w
        EGTNeoWriter.write w grammar
