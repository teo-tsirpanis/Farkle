// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Common

open System.Threading

/// A reference type whose value can only be set once.
type SetOnce< [<ComparisonConditionalOn; EqualityConditionalOn>] 'T> = private {
    mutable _IsSet: int
    mutable _Value: 'T
}
with
    /// Tries to set this `SetOnce`'s value to an object.
    /// Returns whether the value was changed.
    /// This method is thread-safe, in the sense that only
    /// one thread will ever be able to set a value to this object.
    member x.TrySet v =
        if Interlocked.CompareExchange(&x._IsSet, 1, 0) = 0 then
            x._Value <- v
            Thread.MemoryBarrier()
            true
        else
            false
    /// Returns whether this `SetOnce` has a value set.
    member x.IsSet = x._IsSet <> 0
    /// Returns the `SetOnce`'s value - if it is set - or the given object otherwise.
    member x.ValueOrDefault(def) =
        match x._IsSet with
        | 0 -> def
        | _ -> x._Value
    /// Creates a `SetOnce` object whose value can be set at a later time.
    /// (try to guess how many times)
    static member Create() = {
        _IsSet = 0
        _Value = Unchecked.defaultof<_>
    }
    /// Creates a `SetOnce` object whose value is already set and cannot be changed.
    static member Create x = {
        _IsSet = 1
        _Value = x
    }
    override x.ToString() =
        match x._IsSet with
        | 0 -> "(not set)"
        | _ -> x._Value.ToString()

/// Functions to work with the `FSharp.Core.Result` type.
[<AutoOpen>]
module internal Result =

    let tee fOk fError =
        function
        | Ok x -> fOk x
        | Error x -> fError x

    let apply f x =
        match f with
        | Ok f -> x |> Result.map f
        | Error x -> Error x

    /// Consolidates a sequence of `Result`s into a `Result` of a list.
    let collect xs = Seq.foldBack (fun x xs ->
        match x, xs with
        | Ok x, Ok xs -> Ok <| x :: xs
        | Error x, _ -> Error x
        | _, Error _ -> xs) xs (Ok [])

    /// Returns the value of a `Result` or raises an exception.
    let returnOrFail result = tee id (failwithf "%O") result

    /// Returns if the given `Result` succeeded.
    let isOk x = match x with | Ok _ -> true | Error _ -> false

    /// Returns if the given `Result` failed.
    let isError x = match x with | Ok _ -> false | Error _ -> true

    let ofOption = function Some x -> Ok x | None -> Error ()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal List =

    let allSome xs =
        List.foldBack (fun x state ->
            match x, state with
            | Some x, Some state -> Some <| x :: state
            | _, _ -> None) xs (Some [])

[<AutoOpen>]
module internal ErrorHandling =

    /// Raises an exception if `x` is null.
    let inline nullCheck argName x =
        if isNull x then
            nullArg argName
