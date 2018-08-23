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
    let ofFile path = either {
        let path = Path.GetFullPath path
        if path |> File.Exists |> not then
            do! path |> FileNotExist |> Result.Error
        use stream = File.OpenRead path
        return! ofStream stream
    }
