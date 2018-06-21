// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
/// Some useful functions and types that could be used from many points from the library.
module Farkle.Common

open Chessie.ErrorHandling
open FSharpx.Collections

[<Literal>]
/// The line feed character.
let LF = '\010'

[<Literal>]
/// The carriage return character.
let CR = '\013'

/// An active pattern recognizer for the `RandomAccessList`.
let (|RALCons|RALNil|) = RandomAccessList.``|Cons|Nil|``

/// Raises an exception.
/// This function should be used when handling an impossible edge case is very tedious.
/// It should be used __very__ sparingly.
let impossible() = failwith "Hello there! I am a bug. Nice to meet You! If I am here, then something that was thought to be impossible did happen. And if You are (un)lucky enough to see me, can You please open a Github issue? Thank You very much and I am sorry for any inconvenience!"

/// Returns the value of an `Option`.
/// Raises an exception if the option was `None`.
/// Are you inside a `State` monad and don't want to spill your code with `StateResult`?
/// Are you definitely sure that your `Option` does _really_ contain a value, but the type system disagrees?
/// In this case, you should use me!
/// But use me carefully and __very__ sparingly.
/// That thing is like `unsafePerformIO`, but fortunately, not-so-destructive.
let mustBeSome x = x |> Option.defaultWith impossible

/// Ignores the parameter and returns `None`.
let none _ = None

/// Converts a function to curried form.
let curry f x y = f(x, y)

/// Converts a function to uncurried form.
let uncurry f (x, y) = f x y

/// Flips the arguments of a two-parameter curried function.
let flip f x y = f y x

/// Swaps the elements of a pair.
let swap (x, y) = (y, x)

/// Anything that can be indexed.
type Indexable =
    /// The object's index.
    abstract Index: uint32

module Indexable =
    /// Gets the index of an `Indexable` object.
    let index (x: #Indexable) = x.Index
    /// Sorts `Indexable` items based on their index.
    /// Duplicate indices do not raise an error.
    let collect x = x |> Seq.sortBy index |> RandomAccessList.ofSeq

/// A type-safe reference to a value based on its index.
type [<Struct>] Indexed<'a> = Indexed of uint32

/// Functions for working with `Indexed<'a>`.
module Indexed =
    /// Converts an `Indexed` value to an actual object based on an index-retrieving function.
    /// In case the index is not found, the function fails.
    let get (i: Indexed<'a>) (f: _ -> 'a option) =
        let (Indexed i) = i
        match f i with
        | Some x -> ok x
        | None -> fail i

    /// Converts an `Indexed` value to an actual object lased on the index in a specified list.
    let getfromList i list =
        let f i = RandomAccessList.tryNth (int i) list
        get i f

/// An item and its index. A thin layer that makes items `Indexable` without cluttering their type definitions.
type IndexableWrapper<'a> =
    {
        /// The item.
        Item: 'a
        /// And the index.
        Index: uint32
    }
    interface Indexable with
        member x.Index = x.Index

/// Functions to work with `IndexableWrapper`s.       
module IndexableWrapper =

    /// Creates an indexable wrapper
    let create index item = {Index = index; Item = item}

    /// Removes the indexable wrapper of an item.
    let item {Item = x} = x
    
    /// Sorts `Indexable` items based on their index and removes their wrapper.
    /// Duplicate indices do not raise an error.
    let collect x = x |> Seq.sortBy Indexable.index |> Seq.map item |> RandomAccessList.ofSeq

/// A point in 2D space with integer coordinates, suitable for the position of a character in a text.
type Position =
    private Position of (uint32 * uint32)
    with
    override x.ToString() =
        let (Position (x, y)) = x
        sprintf "(%d, %d)" x y

/// Functions to work with the `Position` type.
module Position =

    open LanguagePrimitives

    /// Returns the line of a `Position.
    let line (Position(x, _)) = x

    /// Returns the column of a `Position`.
    let column (Position(_, x)) = x

    /// Returns a `Position` that points at `(1, 1)`.
    let initial = (GenericOne, GenericOne) |> Position

    /// Creates a `Position` at the specified coordinates.
    /// Returns `None` if a coordinate was zero.
    let create line col =
        if line <= GenericZero || col <= GenericZero then
            None
        else
            (line, col) |> Position |> Some

    /// Increases the column index of a `Position` by one.
    let incCol (Position (x, y)) = (x, y + GenericOne) |> Position

    /// Increases the line index of a `Position` by one and resets the collumn to one.
    let incLine (Position(x, _)) = (x + GenericOne, GenericOne) |> Position

/// Some more utilities to work with lists.
module List =

    /// Builds a character list from the given string.
    let ofString (x: string) =
        x.ToCharArray()
        |> List.ofArray

    /// Creates a string from the given character list.
    let toString = Array.ofList >> System.String

/// Some utilities to work with strings
module String =

    /// See `List.toString`.
    let ofList = List.toString

    /// See `List.ofString`.
    let toList = List.ofString

    /// Returns a string that contains the specific string a specified number of times.
    /// The function memoizes the results, so it is better to first give the string argument to the function, and reuse the curried function, if you plan to use it many times.
    let repeat input =
        let dict = System.Collections.Generic.Dictionary()
        let rec impl times =
            match dict.TryGetValue times with
            | true, x -> x
            | false, _ ->
                let x =
                    match times with
                    | 0u -> ""
                    | x when x % 2u = 0u ->
                        let x = impl (x / 2u)
                        x + x
                    | x -> input + impl (x - 1u)
                dict.Add (times, x)
                x
        impl

/// Functions to work with the `Chessie.ErrorHandling.Result` type.
/// I will propably make a PR to add them in the future.
module Trial =

    /// Converts a `Result` to an `option` discarding the messages.
    let makeOption =
        function
        | Ok (x, _) -> Some x
        | Bad _ -> None

    /// Changes the failure type of a `Result`.
    /// A much better alternative of the poorly designed mapFailure function that Chessie provides.
    let mapFailure f =
        function
        | Ok (x, msgs) -> Ok (x, List.map f msgs)
        | Bad msgs -> msgs |> List.map f |> Bad

/// Functions to work with the F# `Choice` type.
module Choice =

    /// Maps the content of a `Choice` with a different function depending on its case.
    let tee2 f1 f2 =
        function
        | Choice1Of2 x -> f1 x
        | Choice2Of2 x -> f2 x

    let private none _ = None

    /// Returns the first case of a `Choice` and `None` if it is on the second.
    let tryChoice1Of2 x = tee2 Some none x

    /// Returns the second case of a `Choice` and `None` if it is on the first.
    let tryChoice2Of2 x = tee2 none Some x
