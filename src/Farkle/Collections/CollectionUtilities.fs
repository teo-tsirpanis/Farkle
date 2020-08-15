// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections.Immutable

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Some extra functions on lists.
module internal List =

    /// Similar to `List.iter2`, but does not throw
    /// an exception when the list lengths are different.
    let rec iter2Safe action list1 list2 =
        match list1, list2 with
        | x1 :: x1s, x2 :: x2s ->
            action x1 x2
            iter2Safe action x1s x2s
        | _, _ -> ()

/// Some extra functions regarding the `ImmutableList` type
module internal ImmutableList =

    /// Adds the specified object to the end of the given immutable list.
    let inline add (xs: ImmutableList<_>) x = xs.Add x
