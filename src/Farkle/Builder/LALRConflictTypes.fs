// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Grammar
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// The reason an LALR conflict failed to be resolved.
type LALRConflictReason =
    /// No prececence info were specified.
    | NoPrecedenceInfo
    /// Precedence info were specified in only one of the two objects.
    | PartialPrecedenceInfo
    /// The objects were specified in different operator scopes.
    | DifferentOperatorScope
    /// The objects have the same precedence but no associativity.
    | PrecedenceOnlySpecified
    /// The productions have the same precedence. This
    /// reason is specified only on Reduce-Reduce conflicts.
    | SamePrecedence
    /// The operator scope cannot resolve Reduce-Reduce conflicts.
    | CannotResolveReduceReduce
    override x.ToString() =
        match x with
        | NoPrecedenceInfo -> "no precedence info were specified"
        | PartialPrecedenceInfo -> "precedence info were specified in only one of the two objects"
        | DifferentOperatorScope -> "the objects were specified in different operator scopes"
        | PrecedenceOnlySpecified -> "the objects had the same precedence but were declared in a \
PrecedenceOnly associativity group"
        | SamePrecedence -> "the productions had the same precedence"
        | CannotResolveReduceReduce -> "the symbols' operator scope cannot resolve Reduce-Reduce conflicts. \
To enable it, pass a boolean value of true to the operator scope's constructor"

/// The type of an LALR conflict.
type LALRConflictType =
    /// A Shift-Reduce conflict
    | ShiftReduce of StateToShiftTo: uint32 * ProductionToReduce: Production
    /// A Reduce-Reduce conflict
    | ReduceReduce of Production1: Production * Production2: Production
with
    /// Creates an `LALRConflictType` from the given conflicting `LALRAction`s.
    /// An exception is raised if the actions are neither both "reduce" nor a "shift" and a "reduce".
    static member Create act1 act2 =
        match act1, act2 with
        | LALRAction.Shift state, LALRAction.Reduce prod
        | LALRAction.Reduce prod, LALRAction.Shift state ->
            ShiftReduce(state, prod)
        | LALRAction.Reduce prod1, LALRAction.Reduce prod2 ->
            ReduceReduce(prod1, prod2)
        | _ -> failwithf "A conflict between %A and %A is impossible" act1 act2
    override x.ToString() =
        match x with
        | ShiftReduce (idx, prod) ->
            sprintf "Shift-Reduce conflict between shifting to state %d and reducing production %O" idx prod
        | ReduceReduce (prod1, prod2) ->
            sprintf "Reduce-Reduce conflict between productions %O and %O" prod1 prod2

/// An LALR conflict.
/// It arises when the parser can take different
/// actions when encountering a `Terminal` or the end.
type LALRConflict = {
    /// The index of the `LALRState` the conflict is taking place.
    StateIndex: uint32
    /// The symbol upon whose encounter, the conflict happens.
    /// `None` means the conflict happens when the parser reaches the end of input.
    [<Nullable(2uy, 1uy)>] Symbol: Terminal option
    /// The type of the conflict.
    Type: LALRConflictType
    /// The reason Farkle could not resolve the conflict.
    Reason: LALRConflictReason
}
with
    /// Creates an `LALRConflict`.
    static member internal Create stateIndex symbol act1 act2 reason = {
        StateIndex = stateIndex
        Symbol = symbol
        Type = LALRConflictType.Create act1 act2
        Reason = reason
    }
    override x.ToString() =
        let symbolAsString =
            match x.Symbol with
            | Some term -> string term
            | None -> "(EOF)"
        sprintf "%O, while encountering the symbol %s at state %d. Farkle could not \
automatically resolve the conflict because %O" x.Type symbolAsString x.StateIndex x.Reason

/// An LALR state that might have a conflict. Its API is
/// almost identical to `Farkle.Grammar.LALRState` type,
/// except that for each terminal or end of input there
/// can be many possible actions.
type LALRConflictState = {
    Index: uint32
    Actions: ImmutableDictionary<Terminal, LALRAction list>
    EOFActions: LALRAction list
    // There can't be a GOTO conflict.
    GotoActions: ImmutableDictionary<Nonterminal, uint32>
}
