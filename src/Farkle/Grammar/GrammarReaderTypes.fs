// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle

type internal DFAState =
    /// This state accepts a symbol. If the state graph cannot be further walked, the included `Symbol` is returned.
    | DFAAccept of index: uint32 * (Symbol * (CharSet * Indexed<DFAState>) list)
    /// This state does not accept a symbol. If the state graph cannot be further walked and an accepting state has not been found, tokenizing fails.
    | DFAContinue of index: uint32 * edges: (CharSet * Indexed<DFAState>) list
    interface Indexable with
        member x.Index =
            match x with
            | DFAAccept (x, _) -> x
            | DFAContinue (x, _) -> x
    override x.ToString() = x :> Indexable |> Indexable.index |> string

module internal DFAState =
    
    let toDFA initial states =
        let extractStates = function | DFAAccept (index, (_, nextStates)) -> index, nextStates | DFAContinue (index, nextStates) -> index, nextStates
        let acceptStates = states |> Seq.choose (function | DFAAccept (index, (symbol, _)) -> Some (index, symbol) | DFAContinue _ -> None) |> Map.ofSeq
        let transition =
            states
            |> Seq.map (extractStates >> (fun (index, nextStates) -> index, nextStates |> Seq.map (fun (cs, Indexed(next)) -> cs, next) |> Map.ofSeq))
            |> Map.ofSeq
        {
            Transition = transition
            InitialState = initial
            AcceptStates = acceptStates
        }

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
type internal LALRState =
    {
        /// The index of the state.
        Index: uint32
        /// The available `LALRAction`s of the state.
        /// Depending on the symbol, the next action to be taken is determined.
        Actions:Map<Symbol, LALRAction>
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() = string x.Index

module internal LALRState =
    let toLALR initial states =
        {
            InitialState = initial
            States = states |> Seq.map (fun {Index = i; Actions = actions} -> i, actions) |> Map.ofSeq
        }