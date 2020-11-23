// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace rec Farkle.Builder

open Farkle.Parser
open System
open System.Reflection

type internal RegexParserResult = Result<Regex, ParserError>

type internal RegexParserFunction = Func<string, RegexParserResult>

[<RequireQualifiedAccess>]
module private RegexUtils =
    let charSetFull = set [char 0 .. char UInt16.MaxValue]
    let isCharSetFull(x: char Set) = x.Count = int UInt16.MaxValue
    let isCharSetHalfFull(x: char Set) = x.Count > int UInt16.MaxValue / 2
    // Taking the concept of circular references to a whole new level.
    #if NET
    [<System.Diagnostics.CodeAnalysis.DynamicDependency("DoParse", "Farkle.Builder.RegexGrammar", "Farkle")>]
    #endif
    let DoParse =
        Assembly
            .GetExecutingAssembly()
            .GetType("Farkle.Builder.RegexGrammar", true)
            .GetMethod("DoParse", BindingFlags.NonPublic ||| BindingFlags.Static)
            .CreateDelegate(typeof<RegexParserFunction>)
        :?> RegexParserFunction

[<RequireQualifiedAccess; NoComparison; StructuralEquality>]
/// <summary>A regular expression that is used to specify a tokenizer symbol.</summary>
/// <remarks>Checking two regular expressions for equality does not mean that they
/// recognize the same symbols, but that their internal structure is the same.</remarks>
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
    /// All characters but these ones.
    // An empty `AllButChars` regex is allowed here, but one with
    // all 65536 characters isn't, so we must be extra careful.
    | AllButChars of char Set
    /// Another Regex, but specified as a string which
    /// will be parsed when a DFA will be generated.
    | RegexString of string * Lazy<RegexParserResult>
    /// A regex that recognizes only the empty string.
    static member Empty = Concat []
    /// <summary>A regex that recognizes any single character
    /// that was not matched by anything else.</summary>
    /// <remarks>Note that it's not the same with "any character". See more at
    /// https://teo-tsirpanis.github.io/Farkle/string-regexes.html#The-dot-regex</remarks>
    static member Any = AllButChars Set.empty
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
    /// <remarks>This is an optimized edition of
    /// <see cref="And"/> for many regexes.</remarks>
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
        // A character has to belong in both sets to not be recognized.
        | AllButChars x1, AllButChars x2 -> AllButChars <| Set.intersect x1 x2
        | AllButChars x1, Chars x2
        | Chars x2, AllButChars x1 -> AllButChars <| x1 - x2
        | Alt x1, Alt x2 -> Alt <| x1 @ x2
        | Alt xs, x
        | x, Alt xs -> Alt <| x :: xs // Alt is commutative.
        | x1, x2 -> Alt [x1; x2]
    /// <summary>Returns a regex that recognizes a string that
    /// is recognized by either of the given regexes.</summary>
    /// <remarks>This is an optimized edition of
    /// <see cref="Or"/> for many regexes.</remarks>
    /// <seealso cref="Or"/>
    static member Choice([<ParamArray>] regexes) =
        match Array.length regexes with
        | 0 -> Regex.Empty
        | 1 -> regexes.[0]
        | _ ->
            let mutable foundAllButChars = false
            let chars = regexes |> Seq.choose (function | Chars x -> Some x | _ -> None) |> Set.unionMany
            let allButChars =
                regexes
                |> Seq.choose (function
                | AllButChars x ->
                    foundAllButChars <- true
                    Some x
                | _ -> None) |> Set.unionMany

            regexes
            |> Seq.ofArray
            |> Seq.choose (function | Chars _ | AllButChars _ -> None | x -> Some x)
            |> Seq.collect (function | Alt x -> x | x -> [x])
            |> Seq.distinct
            |> List.ofSeq
            |> (fun x -> if chars.IsEmpty then x else Chars chars :: x)
            |> (fun x -> if not foundAllButChars || RegexUtils.isCharSetFull allButChars then x else AllButChars allButChars :: x)
            |> function | [x] -> x | x -> Alt x
    /// Returns a regex that recognizes zero or more
    /// strings that are recognized by the given regex.
    member x.ZeroOrMore() =
        match x with
        | Star _ -> x
        | x -> Star x
    /// Returns a regex that recognizes an exact number
    /// of strings that are recognized by the given regex.
    member x.Repeat num = Regex.Join(Array.replicate num x)
    /// Returns a regex that recognizes either the given regex or the empty string.
    member x.Optional() = Regex.Choice(x, Regex.Empty)
    /// Returns a regex that recognizes a ranged number
    /// of strings that are recognized by the given regex.
    /// The range is closed.
    member x.Between from upTo =
        if from > upTo then
            invalidArg "upTo" "'upTo' must be bigger or equal to 'from'"
        Regex.Join(x.Repeat from, x.Optional().Repeat <| upTo - from)
    /// Returns a regex that recognizes at least a number of
    /// consecutive strings that are recognized by the given regex.
    member x.AtLeast num =
        if num = 0 then
            x.ZeroOrMore()
        else
            Regex.Join(x.Repeat num, x.ZeroOrMore())
    /// Returns a regex that only recognizes a literal string.
    static member Literal (str: string) =
        str
        |> Seq.map Regex.Literal
        |> List.ofSeq
        |> Concat
    /// Returns a regex that only recognizes a single literal character.
    static member Literal c = Set.singleton c |> Chars
    /// Returns a regex that recognizes only one character
    /// of these on the given sequence of characters.
    static member OneOf (xs: _ seq) =
        let set =
            match xs with
            | :? Set<char> as x -> x
            | :? PredefinedSet as x -> x.Characters
            | _ -> Set.ofSeq xs
        if set.IsEmpty then
            Regex.Empty
        elif RegexUtils.isCharSetHalfFull set then
            AllButChars <| RegexUtils.charSetFull - set
        else
            Chars set
    /// Returns a regex that recognizes any character,
    /// except of these on the given sequence.
    /// An empty sequence is equivalent to `Regex.Any`.
    static member NotOneOf (xs: _ seq) =
        let set =
            match xs with
            | :? Set<char> as x -> x
            | :? PredefinedSet as x -> x.Characters
            | _ -> Set.ofSeq xs
        if RegexUtils.isCharSetFull set then
            Regex.Empty
        elif RegexUtils.isCharSetHalfFull set then
            Chars <| RegexUtils.charSetFull - set
        else
            AllButChars set
    /// Returns a regex specified by a string.
    /// An invalid regex string will make the building process fail.
    /// See more at https://teo-tsirpanis.github.io/Farkle/string-regexes.html
    static member FromRegexString x =
        let thunk = lazy(RegexUtils.DoParse.Invoke x)
        RegexString(x, thunk)

/// F#-friendly members of the `Regex` class.
/// Please consult the members of the `Regex` class for documentation.
module Regex =

    /// An alias for `Regex.Literal` that takes a character.
    let char (c: char) = Regex.Literal c

    /// An alias for `Regex.OneOf`.
    let chars str = Regex.OneOf str

    /// An alias for `Regex.Any`.
    /// See the documentation of it for caveats.
    let any = Regex.Any

    /// An alias for `Regex.NotOneOf`.
    let allButChars str = Regex.NotOneOf str

    /// An alias for `Regex.Literal` that takes a string.
    let string (str: string) = Regex.Literal str

    let concat xs = Regex.Join(Array.ofSeq xs)

    let choice xs = Regex.Choice(Array.ofSeq xs)

    let repeat num (x: Regex) = x.Repeat num

    let optional (x: Regex) = x.Optional()

    let between from upTo (x: Regex) = x.Between from upTo

    let atLeast num (x: Regex) = x.AtLeast num

    /// An alias for `Regex.ZeroOrMore`.
    /// The name alludes to the Kleene Star.
    let star (x: Regex) = x.ZeroOrMore()

    /// An alias for `atLeast 1`.
    /// The name alludes to the plus symbol of regular expressions.
    let plus x = atLeast 1 x

    let regexString x = Regex.FromRegexString x

[<AutoOpen>]
/// F# operators to easily manipulate `Regex`es.
module RegexOperators =

    /// `Regex.And` as an operator.
    let (<&>) (x1: Regex) x2 = x1.And x2

    /// `Regex.Or` as an operator.
    let (<|>) (x1: Regex) x2 = x1.Or x2
