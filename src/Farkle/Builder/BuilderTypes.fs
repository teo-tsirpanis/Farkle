// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Grammar
open System
open System.Collections
open System.Collections.Generic
open System.Collections.Immutable

/// The type of an LALR conflict.
type LALRConflictType =
    /// A Shift-Reduce conflict
    | ShiftReduce of uint32 * Production
    /// A Reduce-Reduce conflict
    | ReduceReduce of Production * Production
with
    /// Creates an `LALRConflictType` from the given conflicted `LALRAction`s.
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
            sprintf "Shift-Reduce conflict between state %d and production %O" idx prod
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
    Symbol: Terminal option
    /// The type of the conflict.
    Type: LALRConflictType
}
with
    /// Creates an `LALRConflict`.
    static member Create stateIndex symbol act1 act2 = {
        StateIndex = stateIndex
        Symbol = symbol
        Type = LALRConflictType.Create act1 act2
    }
    override x.ToString() =
        let symbolAsString =
            match x.Symbol with
            | Some term -> string term
            | None -> "(EOF)"
        sprintf "%O while encountering the symbol %s at state %d" x.Type symbolAsString x.StateIndex

[<RequireQualifiedAccess>]
/// An error the builder encountered.
type BuildError =
    /// Some symbols cannot be distinguished from each other.
    | IndistinguishableSymbols of DFASymbol Set
    /// Some symbols can contain zero characters.
    | NullableSymbols of DFASymbol Set
    /// An LALR conflict did occur.
    | LALRConflict of LALRConflict Set
    /// Some nonterminals have no productions.
    | EmptyNonterminals of string Set
    /// Some productions are defined twice.
    | DuplicateProductions of (Nonterminal * ImmutableArray<LALRSymbol>) Set
    /// The grammar has more symbols than the supported limit.
    | SymbolLimitExceeded
    /// The maximum number of terminals and nonterminals
    /// a grammar built by Farkle can have.
    /// Currently set to 2^20; sixteen times more of what
    /// GOLD Parser can handle. To be more specific, a
    /// grammar can have at most 2^20 terminals _and_ 2^20
    /// nonterminals.
    // This limitation was imposed to be able to store
    // more informations in the upper bits of an index,
    // in optimized operations. Still, it is a very large
    // number for symbol in grammars.
    static member SymbolLimit = 1 <<< 20
    override x.ToString() =
        match x with
        | IndistinguishableSymbols xs ->
            let symbols = xs |> Seq.map DFASymbol.toString |> String.concat ", "
            sprintf "Cannot distinguish between symbols: %s. \
The conflict is caused when two or more terminal definitions can accept the same text." symbols
        | NullableSymbols xs ->
            let symbols = xs |> Seq.map DFASymbol.toString |> String.concat ", "
            sprintf "The symbols %s can contain zero characters." symbols
        | LALRConflict xs -> xs |> Seq.map string |> String.concat Environment.NewLine
        | EmptyNonterminals xs ->
            xs
            |> Seq.map string
            |> String.concat ", "
            |> sprintf "Nonterminals %s are empty. \
If you want to define a nonterminal with the empty production, you can use \
the production builder called 'empty' (or 'Production.Empty' in C#)."
        | DuplicateProductions xs ->
            xs
            |> Seq.map Production.Format
            |> String.concat ", "
            |> sprintf "Productions %s are defined twice."
        | SymbolLimitExceeded ->
            sprintf "A grammar built by Farkle cannot have \
more than %d terminals or more than %d nonterminals."
                BuildError.SymbolLimit BuildError.SymbolLimit

/// A commonly used set of characters.
type PredefinedSet = private {
    _Name: string
    _CharacterRanges: (char * char) list
    CharactersThunk: Lazy<char Set>
}
with
    static member private CharactersImpl x =
        Seq.collect (fun (cFrom, cTo) -> {cFrom .. cTo}) x |> set
    /// Creates a `PredefinedSet` with the specified name and character ranges.
    static member Create name ranges = {
        _Name = name
        _CharacterRanges = ranges
        CharactersThunk = lazy (PredefinedSet.CharactersImpl ranges)
    }
    /// The set's name. Used for informative purposes.
    member x.Name = x._Name
    /// A sequence of tuples that show the inclusive ranges of characters that belong to this set.
    member x.CharacterRanges = Seq.ofList x._CharacterRanges
    /// The set's characters.
    member x.Characters = x.CharactersThunk.Value
    /// The set's character count.
    member x.Count = x.Characters.Count
    interface IEnumerable with
        member x.GetEnumerator() = (x.Characters :> IEnumerable).GetEnumerator()
    interface IEnumerable<char> with
        member x.GetEnumerator() = (x.Characters :> IEnumerable<_>).GetEnumerator()
    interface IReadOnlyCollection<char> with
        member x.Count = x.Count
