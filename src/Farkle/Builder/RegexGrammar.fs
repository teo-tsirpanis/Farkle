// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<RequireQualifiedAccess>]
module Farkle.Builder.RegexGrammar

open Farkle
open Farkle.Builder.Regex
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection
open System.Text

let private allPredefinedSets =
    let sets =
        Assembly
            .GetExecutingAssembly()
            .GetType("Farkle.Builder.PredefinedSets")
            .GetProperties(BindingFlags.Public ||| BindingFlags.Static)
        |> Seq.filter (fun prop -> prop.DeclaringType = typeof<PredefinedSet>)
        |> Seq.map (fun prop -> prop.GetValue(null) :?> PredefinedSet)
        |> Seq.map (fun x -> KeyValuePair(x.Name, x))
    ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, sets)

let private unescapeString (data: ReadOnlySpan<_>) =
    let sb = StringBuilder(data.Length)
    let mutable i = 0
    while i < data.Length do
        // This trick will allow this function to work with both literal strings
        // and character sets. Two single quotes are always reduced to one. Since
        // multiple character in a set don't matter, we are okay.
        if data.[i] = '\\' || (data.[i] = '\'' && i + 1 < data.Length && data.[i + 1] = '\'') then
            i <- i + 1
        sb.Append(data.[i]) |> ignore
        i <- i + 1
    sb.ToString()

let private unescapeChar (span: ReadOnlySpan<_>) i =
    match span.[i] with
    | '\\' -> span.[i + 1]
    | x -> x

let designtime =
    let escapedChar = choice [
        char '\\' <&> chars "\\]^-"
        any
    ]
    let number = Terminals.genericUnsigned<int> "Number"
    let singleChar =
        terminal "Single Character" (T(fun _ data -> char data.[0])) any
    let predefinedSet =
        char '{' <&> plus any <&> char '}'
        |> terminal "Predefined set" (T(fun _ data ->
            let name = data.Slice(1, data.Length - 2).Trim().ToString()
            match allPredefinedSets.TryGetValue(name) with
            | true, set -> chars set
            | false, _ -> errorf "Cannot found a predefined set named %s." name))
    let oneOfCharacters =
        concat [char '['; plus escapedChar; char ']']
        |> terminal "Character set" (T(fun _ data ->
            unescapeString(data.Slice(1, data.Length - 2)) |> chars))
    let notOneOfCharacters =
        concat [string "[^"; plus escapedChar; char ']']
        |> terminal "All but Character set" (T(fun _ data ->
            unescapeString(data.Slice(2, data.Length - 3)) |> allButChars))
    let oneOfRange =
        concat [char '['; escapedChar; char '-'; escapedChar; char ']']
        |> terminal "Character range" (T(fun _ data ->
            chars [unescapeChar data 1 .. unescapeChar data 3]))
    let notOneOfRange =
        concat [string "[^"; escapedChar; char '-'; escapedChar; char ']']
        |> terminal "All but Character range" (T(fun _ data ->
            allButChars [unescapeChar data 2 .. unescapeChar data 4]))
    let literalString =
        concat [char '\''; plus (string "''" <|> any); char '\'']
        |> terminal "Literal string" (T(fun _ data -> unescapeString(data.Slice(1, data.Length - 2))))

    let regex = nonterminal "Regex"
    let regexQuantified =
        let quantifier = "Quantifier" ||= [
            !& "*" =% star
            !& "+" =% plus
            !& "?" =% optional
            !& "{" .>>. number .>> "}" => repeat
            !& "{" .>>. number .>> "," .>> "}" => atLeast
            !& "{" .>>. number .>> "," .>>. number .>> "}" => between
            empty =% id
        ]
        let regexItem = "Regex item" ||= [
            !& "." =% any
            !& "\d" =% chars Number
            !& "\D" =% allButChars Number
            !& "\s" =% chars Whitespace
            !& "\S" =% allButChars Whitespace
            !& "''" =% char '\''
            !@ literalString => string
            yield! [singleChar; predefinedSet; oneOfCharacters; notOneOfCharacters; oneOfRange; notOneOfRange]
            |> List.map (fun x -> !@ x => id)
            !& "(" .>>. regex .>> ")" => id
        ]
        "Regex quantified" ||= [!@ regexItem .>>. quantifier => (|>)]
    let regexSequence = regexQuantified |> many1 |>> concat |> DesigntimeFarkle.rename "Regex sequence"
    regex.SetProductions(
        !@ regexSequence .>> "|" .>>. regex => (<|>),
        !@ regexSequence => id
    )
    regex
    |> DesigntimeFarkle.caseSensitive true
    |> RuntimeFarkle.markForPrecompile

let runtime = RuntimeFarkle.build designtime
