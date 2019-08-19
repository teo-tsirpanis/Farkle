// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open System

[<RequireQualifiedAccess; NoComparison; StructuralEquality>]
/// <summary>A regular expression that is used to specify a tokenizer symbol.</summary>
/// <remarks>Checking two regular expressions for equality does not mean that they
/// recognize the same symbols, but that their internal strucure is the same.</summary>
type Regex =
    internal
    /// These regexes sorted as one after the other.
    // An empty `Concat` regex is essentially the one that only recognizes the empty string.
    | Concat of Regex list
    /// Either of these regexes.
    // An empty `Alt` regex is essentially the one that does not recognize anything.
    // Because this does not make sense in Farkle, we must be very careful not to
    // create it somewhere.
    | Alt of Regex list
    /// Zero or many times this regex.
    | Star of Regex
    /// One of these characters.
    // A `Chars` regex with an empty set is as bad as an Empty `Alt` regex.
    | Chars of char Set
    /// Returns whether this regular expression can recognize the empty string.
    /// These nullable regexes are rejected by Farkle.
    member x.IsNullable() =
        match x with
        // The empty `Concat` is nullable, by vacuous truth.
        | Concat xs -> xs |> List.forall (fun x -> x.IsNullable())
        | Alt xs -> xs |> List.exists (fun x -> x.IsNullable())
        | Star _ -> true
        | Chars x -> x.IsEmpty
    /// The empty regex. It recognizes only the empty string.
    static member Empty = Concat []
    /// Concatenates two regexes into a new one that recognizes
    /// a string of the first one, and then a string of the second.
    member x1.And x2 =
        match x1, x2 with
        | Concat [], x
        | x, Concat [] -> x
        | Concat x1, Concat x2 -> Concat <| x1 @ x2
        | Concat x1, x2 -> Concat <| x1 @ [x2]
        | x1, Concat x2 -> Concat <| x1 :: x2
        | x1, x2 -> Concat [x1; x2]
    /// <summary>Concatenates many regexes.</summary>
    /// <remarks>This is an optimized edition of <see cref="And"/> for many regexes.</remarks>
    /// <seealso cref="And"/>
    static member Join([<ParamArray>] regexes) =
        match Array.length regexes with
        | 0 -> Regex.Empty
        | 1 -> regexes.[0]
        | _ ->
            regexes
            |> Seq.ofArray
            |> Seq.collect (function | Concat x -> x | x -> [x])
            |> List.ofSeq
            |> Concat
    /// Returns a regex that recognizes either a string of the first or the second given regex.
    member x1.Or x2 =
        match x1, x2 with
        | Chars x1, Chars x2 -> Chars <| Set.union x1 x2
        | Alt x1, Alt x2 -> Alt <| x1 @ x2
        | Alt xs, x
        | x, Alt xs -> Alt <| x :: xs // Alt is commutative.
        | x1, x2 -> Alt [x1; x2]
    /// <summary>Returns a regex that recognizes a string that is recognized by either of the given regexes.<summary>
    /// <remarks>This is an optimized edition of <see cref="Or"/> for many regexes.</remarks>
    /// <seealso cref="Or"/>
    static member Choice([<ParamArray>] regexes) =
        match Array.length regexes with
        | 0 -> Regex.Empty
        | 1 -> regexes.[0]
        | _ ->
            let chars = regexes |> Seq.choose (function | Chars x -> Some x | _ -> None) |> Set.unionMany
            let subtrees =
                regexes
                |> Seq.ofArray
                |> Seq.choose (function | Chars _ -> None | x -> Some x)
                |> Seq.collect (function | Alt x -> x | x -> [x])
                |> Seq.distinct
                |> List.ofSeq
            if chars.IsEmpty then
                subtrees
            else
                Chars chars :: subtrees
            |> function | [x] -> x | x -> Alt x
    /// Returns a regex that recognizes zero or more strings that are recognized by the given regex.
    member x.ZeroOrMore() =
        match x with
        | Star _ -> x
        | x -> Star x
    /// Returns a regex that recognizes an exact number of strings that are recognized by the given regex.
    member x.Repeat num = Regex.Join(Array.replicate num x)
    /// Returns a regex that recognizes either the given regex or the empty string.
    member x.Optional() = Regex.Choice(x, Regex.Empty)
    /// Returns a regex that recognizes a ranged number of strings that are recognized by the given regex.
    /// The range is closed.
    member x.FromTo from upTo =
        if from > upTo then
            invalidArg "upTo" "'upTo' must be bigger or equal to 'from'"
        Regex.Join(x.Repeat from, x.Optional().Repeat <| upTo - from)
    /// Returns a regex that recognizes at least a number of consecutive strings that are recognized by the given regex.
    member x.AtLeast num =
        if num = 0 then
            x.ZeroOrMore()
        else
            Regex.Join(x.Repeat num, x.ZeroOrMore())
    /// Returns a regex that only recognizes a literal string.
    static member Literal (str: string) = str |> Seq.map Regex.Literal |> List.ofSeq |> Concat
    /// Returns a regex that only recognizes a single literal character.
    static member Literal c = Set.singleton c |> Chars
    /// Returns a regex that recognizes only one character of these on the given sequence of characters.
    static member OneOf (xs: _ seq) =
        let set =
            match xs with
            | :? Set<char> as x -> x
            | :? PredefinedSet as x -> x.Characters
            | _ -> Set.ofSeq xs
        if set.IsEmpty then
            Regex.Empty
        else
            Chars set

/// F#-friendly membrs of the `Regex` class.
/// Please consult the members of the `Regex` class for documentation.
module Regex =

    /// [omit]
    let concat xs = Regex.Join(Array.ofSeq xs)

    /// [omit]
    let choice xs = Regex.Choice(Array.ofSeq xs)

    /// [omit]
    let repeat num (x: Regex) = x.Repeat num

    /// [omit]
    let optional (x: Regex) = x.Optional()

    /// [omit]
    let fromTo from upTo (x: Regex) = x.FromTo from upTo

    /// [omit]
    let atLeast num (x: Regex) = x.AtLeast num

    /// [omit]
    let literal (c: char) = Regex.Literal c

    /// [omit]
    let literalS (str: string) = Regex.Literal str

    /// [omit]
    let oneOf str = Regex.OneOf str

[<AutoOpen>]
/// F# operators to easily manipulate `Regex`es.
module RegexOperators =

    /// `Regex.And` as an operator.
    let (<&>) (x1: Regex) x2 = x1.And x2

    /// `Regex.Or` as an operator.
    let (<|>) (x1: Regex) x2 = x1.Or x2
