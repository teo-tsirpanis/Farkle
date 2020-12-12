// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.LALRConflictResolution

open Farkle.Builder
open Farkle.Builder.OperatorPrecedence
open Farkle.Grammar
open System
open System.Collections.Generic
open System.Collections.Immutable

module private TerminalKindComparers =
    let create (comparer: StringComparer) =
        {new EqualityComparer<obj>() with
            member _.Equals(x1, x2) =
                match x1, x2 with
                | :? string as lit1, :? string as lit2 -> comparer.Compare(lit1, lit2) = 0
                | _ -> x1.Equals(x2)
            member _.GetHashCode x =
                match x with
                | :? string as lit -> 2 * comparer.GetHashCode(lit)
                | _ -> 2 * x.GetHashCode() + 1}

    let caseSensitive = create StringComparer.Ordinal

    let caseInsensitive = create StringComparer.OrdinalIgnoreCase

    let get isCaseSensitive = if isCaseSensitive then caseSensitive else caseInsensitive

/// <summary>The decision a <see cref="LALRConflictResolver"/> took.</summary>
type ConflictResolutionDecision =
    /// The resolver chose the first option.
    /// In Shift-Reduce conflicts it means it decided to shift.
    /// In Reduce-Reduce conflicts it means it decided to reduce the first production.
    | ChooseOption1
    /// The resolver chose the second option.
    /// In Shift-Reduce conflicts it means it decided to reduce.
    /// In Reduce-Reduce conflicts it means it decided to reduce the second production.
    | ChooseOption2
    /// The resolver cannot choose an action. The reason is specified.
    | CannotChoose of Reason: LALRConflictReason
    /// Inverts the decusion. Option 1 becomes Option 2 and vice versa.
    /// Otherwise the object is returned unchanged.
    member x.Invert() =
        match x with
        | ChooseOption1 -> ChooseOption2
        | ChooseOption2 -> ChooseOption1
        | CannotChoose _ as x -> x

type private PrecedenceInfo = {
    Precedence: int
    Associativity: AssociativityType
}

/// An object that resolves LALR conflicts. By default
/// its virtual methods fail to perform any resolution.
type LALRConflictResolver() =
    static let defaultResolver = LALRConflictResolver()
    /// Tries to resolve a Shift-Reduce conflict.
    // Thankfully we don't have to bother with EOF because we can't shift on it.
    abstract ResolveShiftReduceConflict: shiftTerminal: Terminal -> reduceProduction: Production -> ConflictResolutionDecision
    default _.ResolveShiftReduceConflict _ _ = CannotChoose NoPrecedenceInfo
    /// Tries to resolve a Reduce-Reduce conflict.
    abstract ResolveReduceReduceConflict: production1: Production -> production2: Production -> ConflictResolutionDecision
    default _.ResolveReduceReduceConflict _ _ = CannotChoose NoPrecedenceInfo
    /// A default resolver that always fails.
    static member Default = defaultResolver

/// A conflict resolver that uses Farkle's operator precedence infrastructure.
type internal PrecedenceBasedConflictResolver(operatorGroups: OperatorGroup seq, terminalMap: IReadOnlyDictionary<_,_>,
        productionMap: IReadOnlyDictionary<_,_>, caseSensitive) =
    inherit LALRConflictResolver()

    static let ChooseShift = ChooseOption1
    static let ChooseReduce = ChooseOption2
    static let ChooseReduce1 = ChooseOption1
    static let ChooseReduce2 = ChooseOption2

    let comparer = TerminalKindComparers.get caseSensitive

    let groupLookup =
        let dict = ImmutableDictionary.CreateBuilder(comparer)
        let mutable i = 0
        for x in operatorGroups do
            for x in x.AssociativityGroups do
                for x in x.Symbols do
                    #if MODERN_FRAMEWORK
                    dict.TryAdd(x, i) |> ignore
                    #else
                    if dict.ContainsKey(x) |> not then
                        dict.Add(x, i)
                    #endif
            i <- i + 1
        dict.ToImmutable()

    let precInfoLookups =
        operatorGroups
        |> Seq.map (fun x ->
            let dict = ImmutableDictionary.CreateBuilder(comparer)
            let mutable prec = 1
            for x in x.AssociativityGroups do
                let precInfo = {Precedence = prec; Associativity = x.AssociativityType}
                for x in x.Symbols do
                    #if MODERN_FRAMEWORK
                    dict.TryAdd(x, precInfo) |> ignore
                    #else
                    if dict.ContainsKey(x) |> not then
                        dict.Add(x, precInfo)
                    #endif
                prec <- prec + 1

            dict.ToImmutable()
        )
        |> Array.ofSeq

    let canResolveReduceReduce =
        operatorGroups
        |> Seq.map (fun x -> x.ResolvesReduceReduceConflict)
        |> Array.ofSeq

    let hasPrecInfo =
        let dict = Dictionary(comparer)
        let rec impl (x: obj) idx =
            idx < precInfoLookups.Length && (precInfoLookups.[idx].ContainsKey x || impl x (idx + 1))
        fun (x: obj) ->
            match dict.TryGetValue x with
            | true, result -> result
            | false, _ ->
                let result = impl x 0
                dict.Add(x, result)
                result

    let getProductionObj =
        let dict = Dictionary(comparer)
        fun {Index = prodIdx; Handle = handle} ->
            // Unless the production has a contextual precedence token,
            // it assumes the P&A of the last terminal it has.
            // This is what (Fs)Yacc does.
            match productionMap.TryGetValue prodIdx with
            | true, prodObj -> ValueSome prodObj
            | false, _ ->
                match dict.TryGetValue prodIdx with
                | true, prodObj -> ValueSome prodObj
                | false, _ ->
                    let mutable lastTerminalPrecInfo = null
                    let mutable i = handle.Length - 1
                    while lastTerminalPrecInfo <> null && i >= 0 do
                        match handle.[i] with
                        | LALRSymbol.Terminal (Terminal(termIdx, _)) when hasPrecInfo terminalMap.[termIdx] ->
                            lastTerminalPrecInfo <- terminalMap.[termIdx]
                        | _ -> ()
                        i <- i - 1

                    dict.Add(prodIdx, lastTerminalPrecInfo)
                    ValueOption.ofObj lastTerminalPrecInfo

    override _.ResolveShiftReduceConflict (Terminal(shiftTerminalIdx, _)) prod =
        match terminalMap.TryGetValue shiftTerminalIdx, getProductionObj prod with
        | (true, termObj), ValueSome prodObj ->
            match groupLookup.TryGetValue termObj, groupLookup.TryGetValue prodObj with
            | (true, termGroup), (true, prodGroup) when termGroup = prodGroup ->
                let group = precInfoLookups.[termGroup]
                // The symbols surely exist in the group.
                let {Precedence = termPrec; Associativity = assoc} = group.[termObj]
                let {Precedence = prodPrec} = group.[prodObj]

                if termPrec < prodPrec then
                    ChooseShift
                elif termPrec = prodPrec then
                    match assoc with
                    | AssociativityType.NonAssociative -> CannotChoose PrecedenceWasNonAssociative
                    | AssociativityType.LeftAssociative -> ChooseReduce
                    | AssociativityType.RightAssociative -> ChooseShift
                else
                    ChooseReduce

            | (true, _), (true, _) -> CannotChoose DifferentOperatorGroup
            | (true, _), (false, _) | (false, _), (true, _) -> CannotChoose PartialPrecedenceInfo
            | (false, _), (false, _) -> CannotChoose NoPrecedenceInfo
        | (true, _), ValueNone | (false, _), ValueSome _ -> CannotChoose PartialPrecedenceInfo
        | (false, _), ValueNone -> CannotChoose NoPrecedenceInfo

    override _.ResolveReduceReduceConflict prod1 prod2 =
        match getProductionObj prod1, getProductionObj prod2 with
        | ValueSome prod1Obj, ValueSome prod2Obj ->
            match groupLookup.TryGetValue prod1Obj, groupLookup.TryGetValue prod2Obj with
            | (true, prod1Group), (true, prod2Group) when prod1Group = prod2Group ->
                let group = precInfoLookups.[prod1Group]
                if canResolveReduceReduce.[prod1Group] then
                    let {Precedence = prod1Prec} = group.[prod1Obj]
                    let {Precedence = prod2Prec} = group.[prod2Obj]

                    if prod1Prec < prod2Prec then
                        ChooseReduce1
                    elif prod1Prec = prod2Prec then
                        CannotChoose SamePrecedence
                    else
                        ChooseReduce2
                else
                    CannotChoose CannotResolveReduceReduce

            | (true, _), (true, _) -> CannotChoose DifferentOperatorGroup
            | (true, _), (false, _) | (false, _), (true, _) -> CannotChoose PartialPrecedenceInfo
            | (false, _), (false, _) -> CannotChoose NoPrecedenceInfo
        | ValueSome _, ValueNone | ValueNone, ValueSome _ -> CannotChoose PartialPrecedenceInfo
        | ValueNone, ValueNone -> CannotChoose NoPrecedenceInfo
