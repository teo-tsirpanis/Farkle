// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.DebugTypeProxies

open Farkle.Collections
open Farkle.Grammars
open System
open System.Diagnostics

[<DebuggerDisplayAttribute("{action,nq}", Name="{symbol,nq}")>]
type internal Action(symbol: string, action: string) = struct end

type internal LALRStateDebugProxy(state: LALRState) =
    let allActions =
        let xs = ResizeArray()
        for KeyValue(sym, action) in state.Actions do
            Action(sym.ToString(), action.ToString()) |> xs.Add
        for KeyValue(sym, gotoAction) in state.GotoActions do
            Action(sym.ToString(), sprintf "Goto %d" gotoAction) |> xs.Add
        match state.EOFAction with
        | Some x -> Action("(EOF)", x.ToString()) |> xs.Add
        | None -> ()
        xs.ToArray()
    [<DebuggerBrowsableAttribute(DebuggerBrowsableState.RootHidden)>]
    member _.AllActions = allActions

type internal RangeMapDebugProxy<'TKey,'TValue when 'TKey:> IComparable<'TKey>>(rm: RangeMap<'TKey,'TValue>) =
    // VSCode's debugger has trouble displaying the members of a span.
    let elements = rm.Elements.ToArray()

    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member _.Elements = elements
