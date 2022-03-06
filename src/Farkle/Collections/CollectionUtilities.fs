// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections.Immutable

/// Some extra functions on lists.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal List =

    /// Similar to `List.iter2`, but does not throw
    /// an exception when the list lengths are different.
    let iter2Safe action list1 list2 =
        let action = OptimizedClosures.FSharpFunc<_,_,_>.Adapt action
        let rec impl list1 list2 =
            match list1, list2 with
            | x1 :: x1s, x2 :: x2s ->
                action.Invoke(x1, x2)
                impl x1s x2s
            | _, _ -> ()
        impl list1 list2

namespace System.Collections.Generic

open System.Runtime.CompilerServices

[<Extension>]
type internal DictionaryShims =
#if !MODERN_FRAMEWORK
    [<Extension>]
    static member TryAdd(dict: IDictionary<_,_>, k, v) =
        if dict.ContainsKey(k) then
            false
        else
            dict.Add(k, v)
            true
#endif
    [<Extension>]
    static member GetOrDefault(dict: IReadOnlyDictionary<_,_>, k, valueDef) =
        match dict.TryGetValue(k) with
        | true, x -> x
        | false, _ -> valueDef
