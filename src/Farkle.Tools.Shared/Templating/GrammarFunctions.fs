// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Farkle.Tools
open Scriban.Runtime
open System
open System.IO
open System.Linq
open System.Text

type IdentifierTypeCase =
    | UpperCase
    | LowerCase
    | CamelCase
    | PascalCase

type GrammarFunctions(g) =

    static let toIdentifier name case (separator: string) =
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

    static let formatProduction printFull {Head = Nonterminal(_, headName); Handle = handle} case separator =
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

    static let shouldPrintFullProduction productions =
        let getFormattingElements prod =
            let handle =
                prod.Handle
                |> Seq.choose (function | LALRSymbol.Terminal term -> Some term | _ -> None)
                |> Array.ofSeq
            prod.Head, handle
        isElementUnique getFormattingElements productions

    let grammar = g.Grammar
    let grammarSO =
        let so = ScriptObject()
        so.Import grammar
        so.SetValue("productions_groupped", grammar.Productions.ToLookup(fun x -> x.Head), true)
        so

    let fShouldPrintFullProduction = shouldPrintFullProduction grammar.Productions

    let bytesThunk = lazy(
        let ext = Path.GetExtension(g.GrammarPath).AsSpan()
        if isGrammarExtension ext then
            File.ReadAllBytes g.GrammarPath
        else
            use stream = new MemoryStream()
            EGT.toStreamNeo stream grammar
            stream.ToArray()
    )

    static member upper_case = UpperCase
    static member lower_case = LowerCase
    static member pascal_case = PascalCase
    static member camel_case = CamelCase

    member _.Grammar = grammarSO
    member _.grammar_path = g.GrammarPath

    member _.fmt (x: obj) case separator =
        match x with
        | :? Terminal as x -> match x with Terminal(_, name) -> toIdentifier name case separator
        | :? Production as x -> formatProduction (fShouldPrintFullProduction x) x case separator
        | _ -> invalidArg "x" (sprintf "Can only format terminals and productions, but got %O instead." <| x.GetType())
    member _.to_base64 doPad =
        let options = if doPad then Base64FormattingOptions.InsertLineBreaks else Base64FormattingOptions.None
        Convert.ToBase64String(bytesThunk.Value, options)

    member x.LoadInstanceMethods(so: ScriptObject) =
        so.Import("fmt", Func<_,_,_,_> x.fmt)
        so.Import("to_base64", Func<_,_> x.to_base64)

    static member extract_group_container {ContainerSymbol = container} =
        match container with
        | Choice1Of2 term -> box term, false
        | Choice2Of2 noise -> box noise, true
        |> (fun (sym, isNoise) -> {|symbol = sym; is_noise = isNoise|})
    static member extract_lalr_action x =
        let so = ScriptObject()
        match x with
        | LALRAction.Shift x -> box x
        | LALRAction.Reduce x -> box x
        | LALRAction.Accept -> null
        |> fun x -> so.SetValue("target", x, true)
        so.Import x
        so
    static member extract_lalr_symbol x =
        match x with
        | LALRSymbol.Terminal x -> box x
        | LALRSymbol.Nonterminal x -> box x
    static member group_dfa_edges {Edges = edges} =
        edges.ToLookup(fun x -> x.Value).OrderBy(fun x -> x.Key)
