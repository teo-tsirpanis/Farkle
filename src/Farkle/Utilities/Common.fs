// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
/// Some useful functions and types that could be used from many points from the library.
module Farkle.Common

open System

let (|RMCons|RMNil|) (x: ReadOnlyMemory<_>) =
    if not x.IsEmpty then
        RMCons (x.Span.Item 0, x.Slice 1)
    else
        RMNil

/// Ignores the parameter and returns `None`.
let inline none _ = None

/// Converts a function to curried form.
let inline curry f x y = f(x, y)

/// Curries and flips the arguments of a function.
let inline yrruc f y x = f(x, y)

/// Converts a function to uncurried form.
let inline uncurry f (x, y) = f x y

/// Flips the arguments of a two-parameter curried function.
let inline flip f x y = f y x

/// Swaps the elements of a pair.
let inline swap (x, y) = (y, x)

/// Faster functions to compare two objects.
module FastCompare =

    /// Compares the first object with another object of the same type.
    /// The types must implement the `IComparable<T>` generic interface.
    /// This function is faster than the F#'s compare methods because it is much less lightweight.
    let inline compare (x1: 'a) (x2: 'a) = (x1 :> IComparable<'a>).CompareTo(x2)

    let inline greater x1 x2 = compare x1 x2 > 0
    let inline greaterOrEqual x1 x2 = compare x1 x2 >= 0

    let inline smaller x1 x2 = compare x1 x2 < 0
    let inline smallerOrEqual x1 x2 = compare x1 x2 <= 0

/// Functions to work with the `FSharp.Core.Result` type.
[<AutoOpen>]
module Result =

    let tee fOk fError =
        function
        | Ok x -> fOk x
        | Error x -> fError x

    let apply f x =
        match f with
        | Ok f -> x |> Result.map f
        | Error x -> Error x

    /// Converts an `option` into a `Result`.
    let failIfNone message =
        function
        | Some x -> Ok x
        | None -> Error message

    /// Returns the value of a `Result` or raises an exception.
    let returnOrFail result = tee id (failwithf "%O") result

    /// Returns if the given `Result` succeeded.
    let isOk x = match x with | Ok _ -> true | Error _ -> false

    /// Returns if the given `Result` failed.
    let isError x = match x with | Ok _ -> false | Error _ -> true

    /// A shorthand operator for `Result.bind`.
    let inline ( >>= ) m f = Result.bind f m

    let inline ( <*> ) f m = apply f m
