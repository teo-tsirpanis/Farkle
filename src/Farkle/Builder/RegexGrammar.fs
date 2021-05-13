// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// <summary>Parses regexes from strings. The language's syntax is exmplained
/// <a href="https://teo-tsirpanis.github.io/Farkle/string-regexes.html">at
/// the project's documentation</a>.</summary>
[<RequireQualifiedAccess>]
module Farkle.Builder.RegexGrammar

open Farkle
open Farkle.Builder.Regex
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Globalization
open System.Reflection
open System.Text

#if NET
open System.Diagnostics.CodeAnalysis

[<DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, "Farkle.Builder.PredefinedSets", "Farkle")>]
#endif
let private allPredefinedSets =
    let sets =
        Assembly
            .GetExecutingAssembly()
            .GetType("Farkle.Builder.PredefinedSets")
            .GetProperties(BindingFlags.Public ||| BindingFlags.Static)
        |> Seq.filter (fun prop -> prop.PropertyType = typeof<PredefinedSet>)
        |> Seq.map (fun prop -> prop.GetValue(null) :?> PredefinedSet)
        |> Seq.map (fun x -> KeyValuePair(x.Name, x))
    ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, sets)

let private unescapeString escapeChar (data: ReadOnlySpan<char>) =
    let sb = StringBuilder(data.Length)
    let mutable i = 0
    while i < data.Length do
        if data.[i] = escapeChar then
            i <- i + 1
        sb.Append(data.[i]) |> ignore
        i <- i + 1
    sb.ToString()

let private parseInt (x: ReadOnlySpan<_>) =
    try
        Int32.Parse(
#if MODERN_FRAMEWORK
            x,
#else
            x.ToString(),
#endif
            NumberStyles.None, NumberFormatInfo.InvariantInfo)
    with
    | e -> error e.Message

[<CompiledName("Designtime")>]
let designtime =
    let escapedChar = char '\\' |> optional <&> any
    let singleChar = terminal "Single character" (T(fun _ data -> char data.[0])) any
    let mkPredefinedSet name start fChars =
        string start <&> plus (allButChars "{}") <&> char '}'
        |> terminal name (T(fun _ data ->
            let name = data.Slice(start.Length, data.Length - start.Length - 1).Trim().ToString()
            match allPredefinedSets.TryGetValue(name) with
            | true, set -> fChars set
            | false, _ -> errorf "Cannot find a predefined set named %s." name))
    let mkCategory name start =
        string start <&> repeat 2 (chars Letter)
        |> terminal name (T(fun _ _ ->
            error "Farkle does not yet support Unicode categories."))
    let mkOneOf name start fChars =
        concat [string start; plus escapedChar; char ']']
        |> terminal name (T(fun _ data ->
            unescapeString '\\' (data.Slice(start.Length, data.Length - start.Length - 1)) |> fChars))
    let mkRange name start fChars =
        concat [string start; escapedChar; char '-'; escapedChar; char ']']
        |> terminal name (T(fun _ data ->
            let idxStart =
                start.Length +
                match data.[start.Length] with
                | '\\' -> 1
                | _ -> 0
            let idxEnd =
                idxStart + 2 +
                match data.[idxStart + 2] with
                | '\\' -> 1
                | _ -> 0
            let cStart = data.[idxStart]
            let cEnd = data.[idxEnd]
            if cEnd < cStart then
                errorf "Range [%c-%c] is out of order." cStart cEnd
            fChars {cStart .. cEnd}))

    let predefinedSet = mkPredefinedSet "Predefined set" "\p{" chars
    let notPredefinedSet = mkPredefinedSet "All but Predefined set" "\P{" allButChars
    let category = mkCategory "Unicode category (unused)" "\p"
    let notCategory = mkCategory "All but Unicode category (unused)" "\P"
    let oneOfCharacters = mkOneOf "Character set" "[" chars
    let notOneOfCharacters = mkOneOf "All but character set" "[^" allButChars
    let oneOfRange = mkRange "Character range" "[" chars
    let notOneOfRange = mkRange "All but Character range" "[^" allButChars
    let literalString =
        concat [char '\''; star (string "''"); allButChars"'"; star (string "''" <|> allButChars "'"); char '\'']
        |> terminal "Literal string" (T(fun _ data ->
            unescapeString '\'' (data.Slice(1, data.Length - 2))))

    let numbers = Number |> chars |> plus
    let quantRepeat =
        concat [char '{'; numbers; char '}']
        |> terminal "Repeat quantifier" (T(fun _ data ->
            parseInt (data.Slice(1, data.Length - 2))
            |> repeat))
    let quantAtLeast =
        concat [char '{'; numbers; string ",}"]
        |> terminal "At least quantifier" (T(fun _ data ->
            parseInt (data.Slice(1, data.Length - 3))
            |> atLeast))
    let quantBetween =
        concat [char '{'; numbers; char ','; numbers; char '}']
        |> terminal "Between quantifier" (T(fun _ data ->
            let data = data.Slice(1, data.Length - 2)
            let commaPos = data.IndexOf ','
            let numFrom = parseInt (data.Slice(0, commaPos))
            let numTo = parseInt (data.Slice(commaPos + 1))
            if numFrom > numTo then
                error "Numbers are out of order in the 'between' quantifier."
            between numFrom numTo))

    let regex = nonterminal "Regex"
    let regexQuantified =
        let quantifier = "Quantifier" ||= [
            !& "*" =% star
            !& "+" =% plus
            !& "?" =% optional
            !@ quantRepeat |> asIs
            !@ quantAtLeast |> asIs
            !@ quantBetween |> asIs
        ]
        let miscLiterals = [
            singleChar
            predefinedSet; notPredefinedSet
            category; notCategory
            oneOfCharacters; notOneOfCharacters
            oneOfRange; notOneOfRange
        ]
        let regexItem = "Regex item" ||= [
            !& "." =% any
            !& "\d" =% chars Number
            !& "\D" =% allButChars Number
            !& "\s" =% chars BuilderCommon.whitespaceCharacters
            !& "\S" =% allButChars BuilderCommon.whitespaceCharacters
            !& "''" =% char '\''
            !& "\\\\" =% char '\\'
            !@ literalString => string
            yield! List.map (fun x -> !@ x |> asIs) miscLiterals
            !& "(" .>>. regex .>> ")" |> asIs
        ]
        let regexQuantified = nonterminal "Regex quantified"
        regexQuantified.SetProductions(
            !@ regexItem |> asIs,
            !@ regexQuantified .>>. quantifier => (|>)
        )
        regexQuantified :> DesigntimeFarkle<_>
    let regexSequence = regexQuantified |> many1 |>> concat |> DesigntimeFarkle.rename "Regex sequence"
    regex.SetProductions(
        !@ regexSequence .>> "|" .>>. regex => (<|>),
        !@ regexSequence |> asIs
    )
    regex
    |> DesigntimeFarkle.caseSensitive true

[<CompiledName("Runtime")>]
let runtime = RuntimeFarkle.build designtime

let internal DoParse x =
    match RuntimeFarkle.parseString runtime x with
    | Ok x -> Ok x
    | Error(FarkleError.ParseError x) -> Error x
    | Error(FarkleError.BuildError x) -> failwithf "Error while building the regex grammar: %O" x

#if DEBUG
// The following line ensures the signature of
// DoParse matches the one of the delegate.
RegexParserFunction DoParse |> ignore
#endif
