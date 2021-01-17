// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Collections
open System.Text
open System.Web

type HtmlFunctions(options) =

    static let isAscii c = c >= char 32 && c <= char 127

    member _.custom_head = options.CustomHeadContent
    static member attr_escape x = HttpUtility.HtmlAttributeEncode x
    static member format_char_range (x: RangeMapElement<char,uint option>) =
        let sb = StringBuilder()
        let printCharacters = isAscii x.KeyFrom && isAscii x.KeyTo
        let formatChar c =
            if printCharacters then
                match c with
                | '<' -> sb.Append "&lt;"
                | '>' -> sb.Append "&gt;"
                | '"' -> sb.Append "&quot;"
                | '&' -> sb.Append "&#39;"
                | '\'' -> sb.Append "&amp;"
                | c -> sb.Append c
            else
                sb.Append("U+").Append((int c).ToString("X4"))
            |> ignore
        if x.KeyFrom = x.KeyTo then
            formatChar x.KeyFrom
        else
            formatChar x.KeyFrom
            sb.Append " .. " |> ignore
            formatChar x.KeyTo
        sb.ToString()
