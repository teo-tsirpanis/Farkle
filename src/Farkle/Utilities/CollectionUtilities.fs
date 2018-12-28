// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

/// Functions to work with the standard F# `list`.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module List =

    /// Returns a list with all its elements existing.
    let allSome x =
        let f x xs =
            match x, xs with
            | Some x, Some xs -> Some (x :: xs)
            | _ -> None
        List.foldBack f x (Some [])

    let popStack n x =
        let rec impl acc n x =
            match x with
            | x :: xs when n >= 1 -> impl (x :: acc) (n - 1) xs
            | x -> acc, x
        impl [] n x
