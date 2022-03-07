// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// The other two parsing libraries do not support syntax checking out
// of the box like Farkle. Their implementation will be placed here.
namespace Farkle.Benchmarks.JsonSyntaxCheckers

// Thankfully FsLexYacc does not require rewriting a complete
// grammar; only its "post-processor" needs to be rewritten.
module FsLexYacc =

    open FSharp.Text.Lexing
    open FsLexYacc.JSON.Lexer
    open FsLexYacc.JSON.Parser

    let private syntaxCheckParserTables =
        let tables = tables
        {tables with
            reductions = Array.replicate tables.reductions.Length (fun _ -> null)}

    let private floatToken = FLOAT (Chiron.Json.Number 0m)
    let private stringToken = STRING ""

    // The "transformer" needs to be rewritten to ensure it does not allocate.
    let rec private syntaxCheckLexerRead lexbuf =
        match _fslex_tables.Interpret(6, lexbuf) with
        | 0 -> syntaxCheckLexerRead lexbuf
        | 1 ->
            lexbuf.StartPos <- lexbuf.StartPos.NextLine
            syntaxCheckLexerRead lexbuf
        | 2 -> floatToken
        | 3 -> TRUE
        | 4 -> FALSE
        | 5 -> NULL
        | 6 -> syntax_check_read_string false lexbuf
        | 7 -> LEFT_BRACE
        | 8 -> RIGHT_BRACE
        | 9 -> LEFT_BRACK
        | 10 -> RIGHT_BRACK
        | 11 -> COLON
        | 12 -> COMMA
        | 13 -> EOF
        | 14 ->
            failwithf "SyntaxError: Unexpected char: '%s' Line: %d Column: %d"
                (LexBuffer<_>.LexemeString lexbuf) (lexbuf.StartPos.Line + 1) lexbuf.StartPos.Column
        | _ -> failwith "read"

    and private syntax_check_read_string ignorequote lexbuf =
        match _fslex_tables.Interpret(0, lexbuf) with
        | 0 ->
            if ignorequote then
                syntax_check_read_string false lexbuf
            else
                stringToken
        | 1 -> syntax_check_read_string true lexbuf
        | 2 -> syntax_check_read_string false lexbuf
        | 3 -> failwith "String is not terminated"
        | _ -> failwith "read_string"

    let parseString x =
        let lexBuf = LexBuffer<_>.FromString x
        syntaxCheckParserTables.Interpret(syntaxCheckLexerRead, lexBuf, 0) |> ignore
        Unchecked.defaultof<Chiron.Json>

    let parseTextReader tr =
        let lexBuf = LexBuffer<_>.FromTextReader tr
        syntaxCheckParserTables.Interpret(syntaxCheckLexerRead, lexBuf, 0) |> ignore
        Unchecked.defaultof<Chiron.Json>

// Because FParsec's "grammars" and "post-processors" are
// tightly coupled, the entire parser has to be rewritten
// to parse JSON but keep nothing. Code adapted from
// Chiron's 6.x.x branch.
module Chiron =

    open FParsec

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

    let jsonParser = preturn (Chiron.Json.Null ()) .>> jsonP
