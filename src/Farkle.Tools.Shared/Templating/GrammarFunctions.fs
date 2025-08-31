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
        | "LrStateMachine"
        | "SpecialNameDefinitions" -> true
        | _ -> false)

    let grammarSO =
        let so = ScriptObject()
        so.Import(grammarObj, filter = grammarMemberFilter)
        so.Import("get_object_from_handle", Func<obj,_>(function
            | :? EntityHandle as h when h.IsTokenSymbol -> TokenSymbolHandle.op_Explicit h |> grammarObj.GetTokenSymbol |> box
            | :? EntityHandle as h when h.IsNonterminal -> NonterminalHandle.op_Explicit h |> grammarObj.GetNonterminal |> box
            | :? EntityHandle as h when h.IsProduction -> ProductionHandle.op_Explicit h |> grammarObj.GetProduction |> box
            | :? TokenSymbolHandle as h -> grammarObj.GetTokenSymbol h |> box
            | :? NonterminalHandle as h -> grammarObj.GetNonterminal h |> box
            | :? ProductionHandle as h -> grammarObj.GetProduction h |> box
            | x -> failwith $"invlid object '{x.GetType()}'; must be a grammar object handle or EntityHandle"))
        so.Import("is_terminal", Func<_,_>(fun x -> grammarObj.IsTerminal x))
        so

    member _.Grammar = grammarSO

    static member is_terminal x = x &&& TokenSymbolAttributes.Terminal <> TokenSymbolAttributes.None
    static member is_group_start x = x &&& TokenSymbolAttributes.GroupStart <> TokenSymbolAttributes.None
    static member is_hidden x = x &&& TokenSymbolAttributes.Hidden <> TokenSymbolAttributes.None
    static member is_noise x = x &&& TokenSymbolAttributes.Noise <> TokenSymbolAttributes.None
    static member is_generated (x: obj) =
        match x with
        | :? TokenSymbolAttributes as x -> x &&& TokenSymbolAttributes.Generated <> TokenSymbolAttributes.None
        | :? NonterminalAttributes as x -> x &&& NonterminalAttributes.Generated <> NonterminalAttributes.None
        | _ -> false

    static member is_ends_on_end_of_input x = x &&& GroupAttributes.EndsOnEndOfInput <> GroupAttributes.None
    static member is_advance_by_character x = x &&& GroupAttributes.AdvanceByCharacter <> GroupAttributes.None
    static member is_keep_end_token x = x &&& GroupAttributes.KeepEndToken <> GroupAttributes.None

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
