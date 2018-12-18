// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<RequireQualifiedAccess>]
/// When the new domain model came out, the EGT reader turned out to be too big to be rewritten.
/// Therefore, it was decided to use a layer that converts the old grammars to the new format.
module internal Farkle.Grammar.Migration

#nowarn "0x06400000"

open Farkle.Collections
open Farkle.Monads.Maybe
open Farkle.Grammar.Legacy
open Farkle.Grammar
open System.Collections.Immutable

let wantTerminal = function | Legacy.Terminal(idx, name) -> Some <| Terminal(idx, name) | _ -> None
let wantNonterminal = function | Legacy.Nonterminal(idx, name) -> Some <| Nonterminal(idx, name) | _ -> None
let wantNoise = function | Legacy.Noise name -> Some <| Noise name | _ -> None
let wantGroupStart = function | Legacy.GroupStart(groupIdx, (_, name)) -> Some <| GroupStart(name, groupIdx.ReInterpret()) | _ -> None
let wantGroupEnd =
    function
    | Legacy.GroupEnd(_, name) -> name |> GroupEnd |> Choice1Of2 |> Some
    | Legacy.Terminal(idx, name) -> Terminal(idx, name) |> Choice2Of2 |> Some
    | _ -> None

let wantProductionHandle =
    function
    | Legacy.Terminal(idx, name) -> Terminal(idx, name) |> Choice1Of2 |> Some
    | Legacy.Nonterminal(idx, name) -> Nonterminal(idx, name) |> Choice2Of2 |> Some
    | _ -> None

let wantGroupContainer =
    function
    | Legacy.Terminal(idx, name) -> Terminal(idx, name) |> Choice1Of2 |> Some
    | Legacy.Noise name -> Noise name |> Choice2Of2 |> Some
    | _ -> None

let wantDFASymbol =
    function
    | Legacy.Terminal(idx, name) -> Terminal(idx, name) |> Choice1Of4 |> Some
    | Legacy.Noise name -> Noise name |> Choice2Of4 |> Some
    | Legacy.GroupStart(groupIdx, (_, name)) -> GroupStart(name, groupIdx.ReInterpret()) |> Choice3Of4 |> Some
    | Legacy.GroupEnd(_, name) -> GroupEnd(name) |> Choice4Of4 |> Some
    | _ -> None

let portSafeArray fPort = Seq.map fPort >> List.ofSeq >> List.allSome >> Option.map (Array.ofList >> SafeArray.ofArrayUnsafe)

let portStateTable fPort {InitialState = s0; States = states} = maybe {
    let! s0 = fPort s0
    let! states = portSafeArray fPort states
    return {InitialState = s0; States = states}
}

let portProduction (x: Legacy.Production) = maybe {
    let! head = wantNonterminal x.Head
    let! handle = x.Handle |> Seq.map wantProductionHandle |> List.ofSeq |> List.allSome |> Option.map ImmutableArray.CreateRange
    return {Index = x.Index; Head = head; Handle = handle}
}

let portGroup (x: Legacy.Group) = maybe {
    let! gc = x.ContainerSymbol |> wantGroupContainer
    let! gs = x.StartSymbol |> wantGroupStart
    let! ge = x.EndSymbol |> wantGroupEnd
    return {
        Name = x.Name
        ContainerSymbol = gc
        Start = gs
        End = ge
        AdvanceMode = match x.AdvanceMode with | Token -> AdvanceMode.Token | Character -> AdvanceMode.Character
        EndingMode = match x.EndingMode with | Open -> EndingMode.Open | Closed -> EndingMode.Closed
        Nesting = x.Nesting |> Set.map (fun idx -> idx.ReInterpret())
    }
}

let portLALRState (x: Legacy.LALRState) = maybe {
    let gotoActions, actions =
        x.Actions
        |> Map.toSeq
        |> Array.ofSeq
        |> Array.partition (function | _, Goto _ -> true | _ -> false)
    let! gotoActions =
        gotoActions
        |> Seq.map (function
            | Legacy.Nonterminal(idx, name), Goto actionIdx ->
                Some <| (Nonterminal(idx, name), actionIdx.ReInterpret())
            | _ -> None)
        |> List.ofSeq
        |> List.allSome
        |> Option.map Map.ofList
    let! actions =
        actions
        |> Seq.map (fun (sym, action) -> maybe {
            let! sym =
                match sym with
                | Legacy.Terminal(idx, name) -> Terminal(idx, name) |> Some |> Some
                | EndOfFile -> Some None
                | _ -> None
            let! action =
                match action with
                | Shift x -> x.ReInterpret() |> LALRAction.Shift |> Some
                | Reduce x -> x |> portProduction |> Option.map LALRAction.Reduce
                | Goto _ -> None
                | Accept -> Some LALRAction.Accept
            return sym, action
        })
        |> List.ofSeq
        |> List.allSome
        |> Option.map Map.ofList
    return {Index = x.Index; Actions = actions; GotoActions = gotoActions}
}

let portDFAState (x: Legacy.DFAState) =
    let fixEdges (x: RangeMap<_, Indexed<_>>) = x |> RangeMap.map (fun x -> x.ReInterpret())
    match x with
    | DFAAccept (idx, (sym, edges)) ->
        sym
        |> wantDFASymbol
        |> Option.map (fun sym -> DFAState.Accept(idx, sym, fixEdges edges))
    | DFAContinue (idx, edges) -> Some <| DFAState.Continue(idx, fixEdges edges)

let migrate (x: Legacy.GOLDGrammar) = maybe {
    let! startSymbol = x._Productions.ItemUnsafe 0u |> Option.bind (fun x -> wantNonterminal x.Head)
    let! nonTerminalInfoMap =
        portSafeArray portProduction x._Productions
        |> Option.map (Seq.groupBy (fun {Head = x} -> x)
            >> Seq.map (fun (nt, prods) -> nt, prods.ToImmutableArray())
            >> Map.ofSeq)
    let! groups = portSafeArray portGroup x._Groups
    let! lalrStates = portStateTable portLALRState x._LALR
    let! dfaStates = portStateTable portDFAState x._DFA
    return {
        _Properties = x._Properties
        _StartSymbol = startSymbol
        _NonterminalInfoMap = nonTerminalInfoMap
        _Groups = groups
        _LALRStates = lalrStates
        _DFAStates = dfaStates
    }
}
