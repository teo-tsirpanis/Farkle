// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module FParsec.JSON.JSONParser

open FParsec

// Because FParsec's "grammars" and "post-processors" are
// tightly coupled, the entire parser has to be rewritten
// to parse JSON but keep nothing. Code adapted from
// Chiron's 6.x.x branch.

let (.>>) (x1: Parser<_,unit>) x2 = x1 .>> x2

module private Escaping =

    let digit i = i >= 0x30 && i <= 0x39
    let hexdig i = digit i || (i >= 0x41 && i <= 0x46) || (i >= 0x61 && i <= 0x66)
    let unescaped i = i >= 0x20 && i <= 0x21 || i >= 0x23 && i <= 0x5b || i >= 0x5d && i <= 0x10ffff
    let unescapedP = skipSatisfy (int >> unescaped)
    let hexdig4P = skipManyMinMaxSatisfy 4 4 (int >> hexdig)
    let escapedP =
        skipChar '\\'
        >>. choice [
            skipChar '"'
            skipChar '\\'
            skipChar '/'
            skipChar 'b'
            skipChar 'f'
            skipChar 'n'
            skipChar 'r'
            skipChar 't'
            skipChar 'u' >>. hexdig4P]
    let charP = choice [unescapedP; escapedP]
    let parse = skipMany charP

let private emp x = Option.defaultValue "" x
let private wsp i = i = 0x20 || i = 0x09 || i = 0x0a || i = 0x0d
let private wspP = skipManySatisfy (int >> wsp)
let private charWspP c = skipChar c .>> wspP
let private beginArrayP = charWspP '['
let private beginObjectP = charWspP '{'
let private endArrayP = charWspP ']'
let private endObjectP = charWspP '}'
let private nameSeparatorP = charWspP ':'
let private valueSeparatorP = charWspP ','
let private jsonP, private jsonR = createParserForwardedToRef ()
let private boolP = skipString "true" <|> skipString "false" .>> wspP
let private nullP = skipString "null" .>> wspP
let private digit1to9 i = i >= 0x31 && i <= 0x39
let private digit i = digit1to9 i || i = 0x30
let private e i = i = 0x45 || i = 0x65
let private minusP = skipChar '-'
let private intP =
    skipChar '0' <|> (skipSatisfy (int >> digit1to9) .>> skipManySatisfy (int >> digit))
let private fracP = skipChar '.' >>.  skipMany1Satisfy (int >> digit)
let private expP =
    skipSatisfy (int >> e)
    >>. optional (skipChar '-' <|> skipChar '+')
    >>. skipMany1Satisfy (int >> digit)
let private numberP =
    optional minusP .>> intP .>> optional fracP .>> optional expP .>> wspP
let private quotationMarkP = skipChar '"'
let private stringP =
    quotationMarkP .>> Escaping.parse .>> quotationMarkP .>> wspP
let private memberP = stringP .>> nameSeparatorP .>> jsonP
let private objectP =
    beginObjectP .>> skipSepBy memberP valueSeparatorP .>> endObjectP
let private arrayP =
    beginArrayP .>> skipSepBy jsonP valueSeparatorP .>> endArrayP

jsonR.Value <- wspP >>. choice [arrayP; boolP; nullP; numberP; objectP; stringP]

let jsonParser = jsonP
