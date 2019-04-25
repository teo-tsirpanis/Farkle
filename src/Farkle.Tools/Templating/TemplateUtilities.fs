// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Scriban.Runtime
open System
open System.Text

type IdentifierTypeCase =
    | UpperCase
    | LowerCase
    | CamelCase
    | PascalCase

module Utilities =

    let toIdentifier name case (separator: string) =
        let sb = StringBuilder()
        let processChar c =
            match c with
            | '\'' -> sb.Append "Apost"
            | '\\' -> sb.Append "Backslash"
            | ' ' -> sb.Append separator
            | '!' -> sb.Append "Exclam"
            | '"' -> sb.Append "Quote"
            | '$' -> sb.Append "Num"
            | '%' -> sb.Append "Dollar"
            | '&' -> sb.Append "Amp"
            | '(' -> sb.Append "LParen"
            | ')' -> sb.Append "RParen"
            | '*' -> sb.Append "Times"
            | '+' -> sb.Append "Plus"
            | ',' -> sb.Append "Comma"
            | '-' -> sb.Append "Minus"
            | '.' -> sb.Append "Dot"
            | '/' -> sb.Append "Div"
            | ':' -> sb.Append "Colon"
            | ';' -> sb.Append "Semi"
            | '<' -> sb.Append "Lt"
            | '=' -> sb.Append "Eq"
            | '>' -> sb.Append "Gt"
            | '?' -> sb.Append "Question"
            | '@' -> sb.Append "At"
            | '[' -> sb.Append "LBracket"
            | ']' -> sb.Append "RBracket"
            | '^' -> sb.Append "Caret"
            | '_' -> sb.Append "UScore"
            | '`' -> sb.Append "Accent"
            | '{' -> sb.Append "LBrace"
            | '|' -> sb.Append "Pipe"
            | '}' -> sb.Append "RBrace"
            | '~' -> sb.Append "Tilde"
            | c -> sb.Append c
            |> ignore
        String.iter processChar name
        if sb.Length > 0 then
            match case with
            | UpperCase -> for i = 0 to sb.Length do sb.[i] <- Char.ToUpperInvariant sb.[i]
            | LowerCase -> for i = 0 to sb.Length do sb.[i] <- Char.ToLowerInvariant sb.[i]
            | PascalCase -> sb.[0] <- Char.ToUpperInvariant sb.[0]
            | CamelCase -> sb.[0] <- Char.ToLowerInvariant sb.[0]
        sb.ToString()

    let formatProduction {Head = head; Handle = handle} case separator =
        let headFormatted = toIdentifier head.Name case separator
        let handleFormatted =
            handle
            |> Seq.choose (function Choice1Of2 term -> Some <| toIdentifier term.Name case separator | Choice2Of2 _ -> None)
            |> List.ofSeq
        headFormatted :: handleFormatted |> String.concat separator

    let toBase64 (grammarBytes: _ []) doPad =
        let options =
            if doPad then Base64FormattingOptions.InsertLineBreaks
            else Base64FormattingOptions.None
        Convert.ToBase64String(grammarBytes, options)

    let load (so: ScriptObject) =
        so.SetValue("upper_case", UpperCase, true)
        so.SetValue("lower_case", LowerCase, true)
        so.SetValue("pascal_case", PascalCase, true)
        so.SetValue("camel_case", CamelCase, true)
        so.Import("fmt", Func<_,_,_,_>(toIdentifier))
        so.Import("fmt_production", Func<_,_,_,_>(formatProduction))
