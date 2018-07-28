// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle
open Farkle.EGTFile
open System.IO

/// Functions to convert EGT files to make a grammar.
module EGT =

    /// Reads a sequence of EGT file bytes and returns a `Grammar`.
    // -----BEGIN RSA PRIVATE KEY-----
    // QlpoOTFBWSZTWRjJetcAADxf8IAQYOeEEIgkhCo///+gCBEQYNQgQAAwAVLVmw1NJmqYhiejQR6gNNAa
    // NMgBEwI0k2plPSNqPU2kA02po9IGQNTFGap6JiDJkDQAAA0afjhmIUHDDNK09F37Ty1xaAxYQZiDEuHA
    // 2JRHfnJEyc6vaastRczrNRAxd6jkbqdI0lyuC8Mnezcc9RJ3ajVd+u63rY/ZS7cq1VYUWBICURkgYmJM
    // gRNikzrUCKc0TF2QyqehAVgKTYGZ0njiYtOHxSbtBelePM7xNG9VgRJznUa4UzZW96K3HQiQU0rUHBgS
    // QBECquqltN55kpJSSwsPAghKSE3EJKDuyxCZgoyLQvVqlUZXDlJzgrTgvKEoGrMJgU2RFONtgJg9lk4D
    // S+7juARvhdnKURnbjZTC1Rh1gnSPRrUgFy0Cmgto9lJP8yJxjpxqktaMWk+M0hLQyAAQSmUahFpngXEg
    // PTX4PrQGiFweCaMzZNRmEQUVoyywfWC0MO1LWhn8XckU4UJAYyXrXA==
    // -----END RSA PRIVATE KEY-----
    [<CompiledName("CreateFromBytes")>]
    let ofBytes x =
        x
        |> EGTReader.readEGT
        >>= GrammarReader.makeGrammar

    /// Reads a stream that represents an EGT file and returns a `Grammar`.
    /// The stream can be disposed when it ends.
    [<CompiledName("CreateFromStream")>]
    let ofStream disposeOnFinish x = x |> Seq.ofByteStream disposeOnFinish |> ofBytes

    /// Reads an EGT file and returns a `Grammar`.
    [<CompiledName("CreateFromFile")>]
    let ofFile path = either {
        let path = Path.GetFullPath path
        if path |> File.Exists |> not then
            do! path |> FileNotExist |> Result.Error
        return! path |> File.ReadAllBytes |> ofBytes
    }
