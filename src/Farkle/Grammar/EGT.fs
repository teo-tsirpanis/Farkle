// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open System.IO

/// Functions to convert EGT files to make a grammar.
module EGT =

    /// Reads a stream that represents an EGT file and returns a `Grammar`.
    /// The stream can be disposed when it ends.
    [<CompiledName("CreateFromStream")>]
    let ofStream stream =
        use r = new BinaryReader(stream)
        EGTReader.readEGT r >>= GrammarReader.read

    /// Reads an EGT file and returns a `Grammar`.
    [<CompiledName("CreateFromFile")>]
    let ofFile path =
        use stream = File.OpenRead path
        ofStream stream

    /// Reads a stream that represents an EGT file and returns a `Grammmar`.
    /// The stream is read with a new engine and can be disposed when it ends.
    let ofStream2 stream =
        use r = new BinaryReader(stream)
        GrammarReader.read2 r

    /// Reads an EGT file and returns a `Grammar`.
    /// The file is read with a new engine.
    let ofFile2 path =
        use stream = File.OpenRead path
        ofStream2 stream
