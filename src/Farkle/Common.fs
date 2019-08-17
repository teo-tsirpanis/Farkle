// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Common

/// Faster functions to compare two objects.
module internal FastCompare =

    open System

    /// Compares the first object with another object of the same type.
    /// The types must implement the `IComparable<T>` generic interface.
    /// This function is faster than the F#'s compare methods because it
    /// avoids the overhead of structural comparisons.
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

    /// A shorthand operator for `Result.bind`.
    let inline ( >>= ) m f = Result.bind f m

    let inline ( <*> ) f m = apply f m
