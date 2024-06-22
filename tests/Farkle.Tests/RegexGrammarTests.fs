// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegexGrammarTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Builder.Regex
open Farkle.Tests

let seqToString x =
    x
    |> Seq.map (fun struct(cFrom, cTo) ->
        let escape c =
            match c with
            | '\\' | ']' | '^' | '-' -> $"\\{c}"
            | _ -> Operators.string c
        if cFrom = cTo then
            escape cFrom
        else
            $"{escape cFrom}-{escape cTo}")
    |> String.concat ""

let rec formatRegex =
    function
    | RegexAny -> "."
    | RegexChars x -> $"[{seqToString x}]"
    | RegexAllButChars x -> $"[^{seqToString x}]"
    | RegexAlt x -> x |> Seq.map formatRegex |> String.concat "|"
    | RegexLoop(x, 0, 1) -> $"{formatQuantifiedRegex x}?"
    | RegexLoop(x, 0, int.MaxValue) -> $"{formatQuantifiedRegex x}*"
    | RegexLoop(x, 1, int.MaxValue) -> $"{formatQuantifiedRegex x}+"
    | RegexLoop(x, m, n) when m = n -> $"{formatQuantifiedRegex x}{{{m}}}"
    | RegexLoop(x, m, n) -> $"{formatQuantifiedRegex x}{{{m},{n}}}"
    | RegexConcat x ->
        x
        |> Seq.map (function | RegexAlt _ as x -> "(" + formatRegex x + ")" | x -> formatRegex x)
        |> String.concat ""
    | RegexRegexString pattern -> $"({pattern})"

and formatQuantifiedRegex =
    function
    | RegexAlt _ | RegexConcat _ as x -> $"({formatRegex x})"
    | x -> formatRegex x

let checkRegex (str: string) regex =
    let regex' =
        expectWantParseSuccess (RegexGrammar.Parser.Parse str) (sprintf "Error while parsing %A" str)
        |> formatRegex
    Expect.equal regex' (formatRegex regex) "The regexes are different"

let mkTest str regex =
    let testTitle = sprintf "%A parses into the correct regex" str
    test testTitle {
        checkRegex str regex
    }

let mkFailedTest str =
    let testTitle = sprintf "%A fails to parse" str
    test testTitle {
        let term =
            regexString str
            |> terminalU "Test"
            |> GrammarBuilder.buildSyntaxCheck
        expectIsParseFailure (term.Parse "") "The regex should not parse"
    }

[<Tests>]
let tests = testList "Regex grammar tests" [
    testProperty "The regex parser works" (fun regex ->
        let regexStr = formatRegex regex
        checkRegex regexStr regex)

    testPropertySmall "The Regex.regexString function works" (fun (RegexStringPair (regex, str)) ->
        let regexStr = formatRegex regex
        let parser =
            regexString regexStr
            |> terminalU "Test"
            |> _.AutoWhitespace(false)
            |> _.BuildSyntaxCheck()
        let result = parser.Parse(str)
        // Ignore build failures where the DFA has too many states.
        result.IsSuccess || result.Error.ToString().Contains("FARKLE0001"))

    yield! [
        "[a\-z]", chars "a-z"
        "\d+", charRanges ['0', '9'] |> plus
        "[^^]", allButChars "^"
        "It's beautiful", string "It's beautiful"
        "\[a-z]", string "[a-z]"
        "[1'2]", chars "1'2"
        @"[\--\\]", charRanges ['-', '\\']
        "[\^-|]", charRanges ['^', '|']
        "[^\^-|]", allButCharRanges ['^', '|']
        "[\^\-|]", chars "^-|"
        "(.{59})?", any |> repeat 59 |> optional
        "0059", string "0059"
        ".{5,9}", between 5 9 any
        ".{59,}", atLeast 59 any
        ".\{59}?", concat [any; string "{59"; char '}' |> optional]
        "\.", char '.'
        "[.]", char '.'
        @"\\d", string "\d"
        @"\\\\\\", string "\\\\\\"
        "[0-246-8]", charRanges ['0', '2'; '4', '4'; '6', '8']
        "[+-]", chars "+-" // We must use a character earlier than '-' in ASCII.
        "[]]", char ']'
        "[^]]", allButChars [']']
    ]
    |> List.map ((<||) mkTest)

    yield! [
        "[]"
        "["
        "[^"
        "[^]"
        "[z-a]"
        "\p{Number}"
        "\P{Number}"
        "\pL"
        "\PL"
        "("
        "x{}"
        "x{9999999999}"
        "x{2,1}"
    ]
    |> List.map mkFailedTest
]
