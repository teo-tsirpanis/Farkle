// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegexGrammarTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.Regex
open Farkle.Grammar
open Farkle.Tests

let seqToString x =
    x
    |> Seq.map (fun c ->
        // All but the first character need to be escaped
        // only under specific circumstances, but we will be less discrete.
        if c = '\\' || c = ']' || c = '^' || c = '-' then
            sprintf "\\%c" c
        else
            Operators.string c)
    |> String.concat ""

let rec formatRegex =
    function
    | Regex.Chars x -> sprintf "[%s]" (seqToString x)
    | Regex.AllButChars x -> sprintf "[^%s]" (seqToString x)
    | Regex.Star(Regex.Concat _ as x)
    | Regex.Star(Regex.Alt _ as x) -> "(" + formatRegex x + ")*"
    | Regex.Star x -> formatRegex x + "*"
    | Regex.Alt x -> x |> Seq.map formatRegex |> String.concat "|"
    | Regex.Concat x ->
        x
        |> Seq.map (function | Regex.Alt _ as x -> "(" + formatRegex x + ")" | x -> formatRegex x)
        |> String.concat ""
    | Regex.RegexString holder -> "(" + holder.RegexString + ")"

let checkRegex str regex =
    let regex' =
        RuntimeFarkle.parseString RegexGrammar.runtime str
        |> Flip.Expect.wantOk (sprintf "Error while parsing %A" str)
    Expect.equal regex' regex "The regexes are different"

let mkTest str regex =
    let testTitle = sprintf "%A parses into the correct regex" str
    test testTitle {
        checkRegex str regex
    }

[<Tests>]
let tests = testList "Regex grammar tests" [
    testProperty "The regex parser works" (fun regex ->
        let regexStr = formatRegex regex
        checkRegex regexStr regex)

    testPropertySmall "The Regex.regexString function works" (fun (RegexStringPair (regex, str)) ->
        let regexStr = formatRegex regex
        let dfa =
            [regexString regexStr, Choice1Of4 <| Terminal(0u, "Test")]
            |> DFABuild.buildRegexesToDFA true false
            |> Flip.Expect.wantOk "Building DFA failed"
        matchDFAToString dfa str |> Option.isSome)

    yield! [
        "[a\-z]", chars "a-z"
        "\d+", Number |> chars |> plus
        "[^^]", allButChars "^"
        "''", char '\''
        "'It''s beautiful'", string "It's beautiful"
        "It''s beautiful", string "It'sbeautiful"
        "'[a-z]'", string "[a-z]"
        "[1'2]", chars "1'2"
        "\p{Number}", chars Number
        @"[\--\\]", chars ['-' .. '\\']
        "[\^-|]", chars ['^' .. '|']
        "[^\^-|]", allButChars ['^' .. '|']
        "[\^\-|]", chars "^-|"
        ".{59}?", any |> repeat 59 |> optional
        "0059", string "0059"
        ".{5,9}", between 5 9 any
        ".{59,}", atLeast 59 any
        ".'{'59}?", concat [any; string "{59"; char '}' |> optional]
        "'.'", char '.'
        "[.]", char '.'
        "' '", char ' '
        @"'\\d'", string "\d"
    ]
    |> List.map ((<||) mkTest)
]
