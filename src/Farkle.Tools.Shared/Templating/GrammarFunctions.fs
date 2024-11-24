// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammars
open Farkle.Tools
open Scriban.Runtime
open System
open System.Linq
open System.Text

type IdentifierTypeCase =
    | UpperCase
    | LowerCase
    | CamelCase
    | PascalCase

type GrammarFunctions(g: GrammarTemplateInput) =

    let grammarObj = g.Grammar

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
            | UpperCase -> for i = 0 to sb.Length do sb[i] <- Char.ToUpperInvariant sb[i]
            | LowerCase -> for i = 0 to sb.Length do sb[i] <- Char.ToLowerInvariant sb[i]
            | PascalCase -> sb[0] <- Char.ToUpperInvariant sb[0]
            | CamelCase -> sb[0] <- Char.ToLowerInvariant sb[0]
        sb.ToString()

    let formatProduction printFull (p: Production) case separator =
        let headFormatted = toIdentifier p.Head.Name case separator
        let handleFormatted =
            if p.Members.Count = 0 then
                // GOLD Parser doesn't do that, but specifying "Empty" increases readability.
                ["Empty"]
            else
                p.Members
                |> Seq.choose (fun x ->
                    if x.IsTokenSymbol then
                        let term = grammarObj.GetTokenSymbol <| TokenSymbolHandle.op_Explicit x
                        Some <| toIdentifier term.Name case separator
                    // We might want to include even the nonterminals in
                    // the name, when names collide, but only then.
                    elif printFull then
                        let nont = grammarObj.GetNonterminal <| NonterminalHandle.op_Explicit x
                        Some <| toIdentifier nont.Name case separator
                    else
                        None)
                |> List.ofSeq
        headFormatted :: handleFormatted |> String.concat separator

    let shouldPrintFullProduction productions =
        let getFormattingElements (prod: Production) =
            let handle =
                prod.Members
                |> Seq.choose (fun x -> if x.IsTokenSymbol then Some x else None)
                |> Array.ofSeq
            prod.Head, handle
        isElementUnique getFormattingElements productions

    static let grammarMemberFilter = MemberFilterDelegate(fun mi ->
        match mi.Name with
        | "GrammarInfo"
        | "Terminals"
        | "TokenSymbols"
        | "Groups"
        | "Nonterminals"
        | "Productions"
        | "DfaOnChar"
        | "LrStateMachine" -> true
        | _ -> false)

    let grammarSO =
        let so = ScriptObject()
        so.Import(grammarObj, filter = grammarMemberFilter)
        so.SetValue("productions_grouped", grammarObj.Productions.ToLookup(_.Head), true)
        so

    let fShouldPrintFullProduction = shouldPrintFullProduction grammarObj.Productions

    static member upper_case = UpperCase
    static member lower_case = LowerCase
    static member pascal_case = PascalCase
    static member camel_case = CamelCase

    member _.Grammar = grammarSO

    member _.fmt (x: obj) case separator =
        match x with
        | :? TokenSymbol as x -> toIdentifier x.Name case separator
        | :? Production as x -> formatProduction (fShouldPrintFullProduction x) x case separator
        | _ -> invalidArg "x" (sprintf "Can only format token symbols and productions, but got %O instead." <| x.GetType())
    static member group_dfa_edges (state: StateMachines.DfaState<char>) =
        state.Edges.ToLookup(_.Target).OrderBy(_.Key)
    member _.is_conflict_report =
        match grammarObj.DfaOnChar, grammarObj.LrStateMachine with
        | dfa, _ when dfa <> null && dfa.HasConflicts -> true
        | _, lr when lr <> null && lr.HasConflicts -> true
        | _, _ -> false
    member _.grammar_path = g.GrammarPath
    member _.to_base64 doPad =
        let options = if doPad then Base64FormattingOptions.InsertLineBreaks else Base64FormattingOptions.None
        Convert.ToBase64String(grammarObj.Data.ToArray(), options)

    member x.LoadInstanceMethods (so: ScriptObject) =
        so.Import("fmt", Func<_,_,_,_> x.fmt)
        so.Import("to_base64", Func<_,_> x.to_base64)
