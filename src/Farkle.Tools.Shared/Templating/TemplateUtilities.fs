// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Farkle.Tools
open Scriban
open Scriban.Runtime
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Text
open System.Threading.Tasks
open System.Web

type private IdentifierTypeCase =
    | UpperCase
    | LowerCase
    | CamelCase
    | PascalCase

type private FarkleObject = {Version: string}

module internal Utilities =

    let private defaultFarkleObject = {Version = toolsVersion}

    let private addReadOnly (so: ScriptObject) key value =
        so.SetValue(key, value, true)

    let private toBase64 {Grammar = grammar; GrammarPath = grammarPath} =
        let bytesThunk = lazy(
            let ext = Path.GetExtension(grammarPath).AsSpan()
            if isGrammarExtension ext then
                File.ReadAllBytes grammarPath
            else
                use stream = new MemoryStream()
                EGT.toStreamNeo stream grammar
                stream.ToArray()
        )
        fun doPad ->
            let options =
                if doPad then Base64FormattingOptions.InsertLineBreaks
                else Base64FormattingOptions.None
            Convert.ToBase64String(bytesThunk.Value, options)

    let private toIdentifier name case (separator: string) =
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
        for c in name do
            processChar c
        if sb.Length > 0 then
            match case with
            | UpperCase -> for i = 0 to sb.Length do sb.[i] <- Char.ToUpperInvariant sb.[i]
            | LowerCase -> for i = 0 to sb.Length do sb.[i] <- Char.ToLowerInvariant sb.[i]
            | PascalCase -> sb.[0] <- Char.ToUpperInvariant sb.[0]
            | CamelCase -> sb.[0] <- Char.ToLowerInvariant sb.[0]
        sb.ToString()

    let private formatProduction printFull {Head = Nonterminal(_, headName); Handle = handle} case separator =
        let headFormatted = toIdentifier headName case separator
        let handleFormatted =
            if handle.IsEmpty then
                // GOLD Parser doesn't do that, but specifying "Empty" increases readability.
                ["Empty"]
            else
                handle
                |> Seq.choose (function
                    | LALRSymbol.Terminal (Terminal(_, name)) -> Some <| toIdentifier name case separator
                    // We might want to include even the nonterminals in
                    // the name, when names collide, but only then.
                    | LALRSymbol.Nonterminal (Nonterminal(_, name)) when printFull -> Some <| toIdentifier name case separator
                    | LALRSymbol.Nonterminal _ -> None)
                |> List.ofSeq
        headFormatted :: handleFormatted |> String.concat separator

    let private shouldPrintFullProduction productions =
        let getFormattingElements prod =
            let handle =
                prod.Handle
                |> Seq.choose (function | LALRSymbol.Terminal term -> Some term | _ -> None)
                |> Array.ofSeq
            prod.Head, handle
        let dict =
            productions
            |> Seq.groupBy getFormattingElements
            |> Seq.collect (fun (_, terms) ->
                match Array.ofSeq terms with
                // This case is actually impossible, but never mind.
                | [| |] -> Seq.empty
                // If a production has a unique combination of head and
                // terminal handles, it will also have a unique name.
                | [|prod|] -> KeyValuePair(prod, false) |> Seq.singleton
                // But if many productions share the same one, we will have
                // a name collision. In this case, we want their name to include
                // nonterminals as well. It is impossible for a collision to happen
                // again, as that would imply that there are two identical productions.
                | prods -> prods |> Seq.map (fun prod -> KeyValuePair(prod, true)))
            |> ImmutableDictionary.CreateRange
        fun prod -> dict.[prod]

    let private doFmt fShouldPrintFullProduction (x: obj) case separator =
        match x with
        | :? Terminal as x -> match x with Terminal(_, name) -> toIdentifier name case separator
        | :? Production as x -> formatProduction (fShouldPrintFullProduction x) x case separator
        | _ -> invalidArg "x" (sprintf "Can only format terminals and productions, but got %O instead." <| x.GetType())

    let private builtinPrefix = "builtin://"

    let private htmlTemplateLoader = {new ITemplateLoader with
        member _.GetPath(_, _, templatePath) =
            if templatePath.StartsWith(builtinPrefix) then
                sprintf "Html.%s.scriban" (templatePath.Substring(builtinPrefix.Length))
            else
                null
        member _.Load(_, _, templatePath) =
            ResourceLoader.load templatePath
        member _.LoadAsync(_, _, templatePath) =
            ResourceLoader.load templatePath |> ValueTask<_>}

    let loadHtml {CustomHeadContent = head} (tc: TemplateContext) (so: ScriptObject) =
        tc.TemplateLoader <- htmlTemplateLoader
        addReadOnly so "custom_head" head
        so.Import("attr_escape", Func<_,_> HttpUtility.HtmlAttributeEncode)

    let loadGrammar g so =
        addReadOnly so "upper_case" UpperCase
        addReadOnly so "lower_case" LowerCase
        addReadOnly so "pascal_case" PascalCase
        addReadOnly so "camel_case" CamelCase

        let grammar = g.Grammar
        addReadOnly so "gramamr" grammar
        addReadOnly so "grammar_path" g.GrammarPath
        so.Import("fmt", Func<_,_,_,_> (doFmt <| shouldPrintFullProduction grammar.Productions))
        so.Import("to_base_64", Func<_,_>(toBase64 g))

    let createDefaultScriptObject() =
        let so = ScriptObject()
        addReadOnly so "farkle" defaultFarkleObject
        so
