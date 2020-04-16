// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

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

let private escapeLiteralString = T(fun _ data ->
    let data = data.Slice(1, data.Length - 2)
    let sb = StringBuilder(data.Length)
    let mutable i = 0
    while i < data.Length do
        match data.[i] with
        | '\'' ->
            i <- i + 1
            sb.Append('\'')
        | c -> sb.Append(c)
        |> ignore
        i <- i + 1
    sb.ToString())

let designtime =
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
        concat [char '['; plus any; char ']']
        |> terminal "Character set" (T(fun _ data ->
            data.Slice(1, data.Length - 2).ToString() |> chars))
    let notOneOfCharacters =
        concat [string "[^"; plus any; char ']']
        |> terminal "All but Character set" (T(fun _ data ->
            data.Slice(2, data.Length - 3).ToString() |> allButChars))
    let oneOfRange =
        concat [char '['; any; char '-'; any; char ']']
        |> terminal "Character range" (T(fun _ data ->
            chars [data.[1] .. data.[3]]))
    let notOneOfRange =
        concat [string "[^"; any; char '-'; any; char ']']
        |> terminal "All but Character range" (T(fun _ data ->
            allButChars [data.[2] .. data.[4]]))
    let literalString =
        concat [char '\''; plus (string "''" <|> any); char '\'']
        |> terminal "Literal string" escapeLiteralString

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
