// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammars
open Scriban.Runtime
open System
open System.Linq

type GrammarFunctions(g: GrammarTemplateInput) =

    let grammarObj = g.Grammar

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

    member _.Grammar = grammarSO

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
        so.Import("to_base64", Func<_,_> x.to_base64)
