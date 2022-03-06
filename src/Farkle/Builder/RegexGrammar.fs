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
open System.Globalization
open System.Reflection
open System.Text

#if NET
open System.Diagnostics.CodeAnalysis

[<DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, "Farkle.Builder.PredefinedSets", "Farkle")>]
#endif
let private allPredefinedSets =
    let dict = Dictionary(StringComparer.OrdinalIgnoreCase)
    Assembly
        .GetExecutingAssembly()
        .GetType("Farkle.Builder.PredefinedSets")
        .GetProperties(BindingFlags.Public ||| BindingFlags.Static)
    |> Seq.filter (fun prop -> prop.PropertyType = typeof<PredefinedSet>)
    |> Seq.iter (fun prop ->
        let set = prop.GetValue(null) :?> PredefinedSet
        dict.Add(set.Name, set)
        if set.Name <> prop.Name then
            dict.Add(prop.Name, set))
    dict

[<Struct>]
type private ParseCharSetState =
    | Empty
    | HasChar of previousCharacter: char
    | HasDash of characterBeforeDash: char

let private addRange cFrom cTo i (xs: ResizeArray<_>) (pos: inref<Position>) (data: ReadOnlySpan<_>) =
    if cFrom <= cTo then
        xs.AddRange {cFrom .. cTo}
    else
        // i stores the index of the last character or the last backslash.
        // We need to fail at the dash, we know the dash is one place before the
        // last character or backslash, and so we subtract one from i.
        let errorPos = pos.Advance(data.Slice(0, i - 1))
        ParserApplicationException("Character range is out of order.", errorPos)
        |> raise

let rec private parseCharSet_impl i state (xs: ResizeArray<_>) (pos: inref<_>) (data: ReadOnlySpan<_>) =
    if i = data.Length then
        match state with
        | Empty -> ()
        | HasChar x -> xs.Add x
        | HasDash x -> xs.Add '-'; xs.Add x
        seq xs
    else
        match state, data.[i] with
        | Empty, '\\' ->
            parseCharSet_impl (i + 2) (HasChar data.[i + 1]) xs &pos data
        | Empty, x ->
            parseCharSet_impl (i + 1) (HasChar x) xs &pos data
        | HasChar c, '\\' ->
            xs.Add c
            parseCharSet_impl (i + 2) (HasChar data.[i + 1]) xs &pos data
        | HasChar c, '-' ->
            parseCharSet_impl (i + 1) (HasDash c) xs &pos data
        | HasChar c, x ->
            xs.Add c
            parseCharSet_impl (i + 1) (HasChar x) xs &pos data
        | HasDash cFrom, '\\' ->
            let cTo = data.[i + 1]
            addRange cFrom cTo i xs &pos data
            parseCharSet_impl (i + 2) Empty xs &pos data
        | HasDash cFrom, cTo ->
            addRange cFrom cTo i xs &pos data
            parseCharSet_impl (i + 1) Empty xs &pos data

let private parseCharSet startingPosition pos data =
    parseCharSet_impl startingPosition Empty (ResizeArray()) &pos data

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
    let mkCharSet name start fChars =
        concat [
            string start
            [
                char '\\' <&> chars ['-'; '\\'; ']'; '^']
                any
            ] |> choice |> plus
            char ']'
        ]
        |> terminal name (T(fun ctx data ->
            // We trim only the end of the span but instruct the parser
            // to begin after the starting characters. We keep them on
            // the span for accurate error position reporting.
            let data = data.Slice(0, data.Length - 1)
            parseCharSet start.Length ctx.StartPosition data |> fChars))

    let predefinedSet = mkPredefinedSet "Predefined set" "\p{" chars
    let notPredefinedSet = mkPredefinedSet "All but Predefined set" "\P{" allButChars
    let category = mkCategory "Unicode category (unused)" "\p"
    let notCategory = mkCategory "All but Unicode category (unused)" "\P"
    let charSet = mkCharSet "Character set" "[" chars
    let notCharSet = mkCharSet "All but Character set" "[^" allButChars
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
            charSet; notCharSet
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
    :> DesigntimeFarkle<_>
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
