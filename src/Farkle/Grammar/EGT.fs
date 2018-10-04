// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open System
open System.IO

/// Functions to read EGT files to make a grammar.
module EGT =

    /// Reads a stream that represents an EGT file and returns a `Grammar`.
    [<CompiledName("CreateFromStream")>]
    let ofStream stream =
        use r = new BinaryReader(stream)
        GrammarReader.read r

    /// Reads a Base64-encoded string of the EGT file and returns a grammar.
    [<CompiledName("CreateFromBase64String")>]
    let ofBase64String x =
        let x = Convert.FromBase64String x
        use s = new MemoryStream(x)
        ofStream s

    /// Reads an EGT file from the file system and returns a `Grammar`.
    [<CompiledName("CreateFromFile")>]
    let ofFile path =
        use stream = File.OpenRead path
        ofStream stream
