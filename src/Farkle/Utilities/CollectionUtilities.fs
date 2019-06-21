// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

/// Functions to work with the standard F# `list`.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal List =

    let popStack n x =
        let rec impl acc n x =
            match x with
            | x :: xs when n >= 1 -> impl (x :: acc) (n - 1) xs
            | x -> acc, x
        impl [] n x
