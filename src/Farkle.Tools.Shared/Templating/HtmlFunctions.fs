// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Collections
open System
open System.Text
open System.Web

type HtmlFunctions(options) =

    static let isAscii c = c >= char 32 && c <= char 126

    member _.custom_head = options.CustomHeadContent
    static member attr_escape x = HttpUtility.HtmlAttributeEncode x
    static member format_char_range (x: RangeMapElement<char,uint option>) =
        let sb = StringBuilder()
        let formatChar forceCodepoint c =
            match c with
            | '\t' -> sb.Append "<span class=\"special-char\">Tab</span>"
            | '\n' -> sb.Append "<span class=\"special-char\">Line Feed</span>"
            | '\r' -> sb.Append "<span class=\"special-char\">Carriage Return</span>"
            | ' ' -> sb.Append "<span class=\"special-char\">Space</span>"
            | '"' -> sb.Append "&quot;"
            | '&' -> sb.Append "&amp;"
            | '\'' -> sb.Append "&#39;"
            | ',' -> sb.Append "<span class=\"special-char\">Comma</span>"
            | '<' -> sb.Append "&lt;"
            | '>' -> sb.Append "&gt;"
            | c when not forceCodepoint && isAscii c -> sb.Append c
            | c -> 
                let codePointStr = (int c).ToString("X4")
                if Char.IsControl c then
                    sb.Append("U+").Append(codePointStr)
                else
                    sb.Append("<span title=\"&#x").Append(codePointStr).Append("\">U+").Append(codePointStr).Append("</span>")
            |> ignore
        if x.KeyFrom = x.KeyTo then
            formatChar false x.KeyFrom
        else
            let forceCodepoint = isAscii x.KeyFrom <> isAscii x.KeyTo
            formatChar forceCodepoint x.KeyFrom
            if int x.KeyTo - int x.KeyFrom = 1 then ", " else "…"
            |> sb.Append |> ignore
            formatChar forceCodepoint x.KeyTo
        sb.ToString()
