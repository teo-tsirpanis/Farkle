// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Common

open System
open System.Threading

/// Faster functions to compare two objects.
module internal FastCompare =

    /// Compares the first object with another object of the same type.
    /// The types must implement the `IComparable<T>` generic interface.
    /// This function is faster than the F#'s compare methods because it
    /// avoids the overhead of structural comparisons.
    let inline compare (x1: 'a) (x2: 'a) = (x1 :> IComparable<'a>).CompareTo(x2)

    let inline greater x1 x2 = compare x1 x2 > 0
    let inline greaterOrEqual x1 x2 = compare x1 x2 >= 0

    let inline smaller x1 x2 = compare x1 x2 < 0
    let inline smallerOrEqual x1 x2 = compare x1 x2 <= 0

/// A reference type whose value can only be set once.
type SetOnce< [<ComparisonConditionalOn; EqualityConditionalOn>] 'T> = private {
    mutable _IsSet: int
    mutable _Value: 'T
}
with
    /// Tries to set this `SetOnce`'s value to an object.
    /// Returns whether the value was changed.
    /// This method is thread-safe.
    member x.TrySet v =
        if Interlocked.CompareExchange(&x._IsSet, 1, 0) = 0 then
            x._Value <- v
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
