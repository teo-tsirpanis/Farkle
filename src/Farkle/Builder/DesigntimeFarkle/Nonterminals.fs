// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
open System
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// <summary>The base, untyped interface of <see cref="Production{T}"/>.</summary>
/// <seealso cref="Production{T}"/>
// This type's naming differs from the other interfaces, because there is
// an module that must be called `Production` (so that it has a C#-friendly name).
type internal AbstractProduction =
    /// The members of the production.
    abstract Members: DesigntimeFarkle ImmutableArray
    /// The fuser to process the members of this production.
    abstract Fuser: FuserData
    /// An object representing this production in an
    /// `OperatorScope`, providing contextual precedence.
    abstract ContextualPrecedenceToken: obj

/// <summary>A production. Productions are parts of <see cref="Nonterminal{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this production generates.</typeparam>
/// <remarks>User code must not implement this interface, or an exception might be thrown.</remarks>
type Production<[<Nullable(2uy); CovariantOut>] 'T> = interface end

[<Sealed>]
type internal ProductionConcrete<[<Nullable(2uy)>] 'T>(members: _ seq, fuser: FuserData, cpToken) =
    let members = members.ToImmutableArray()
    interface AbstractProduction with
        member _.Members = members
        member _.Fuser = fuser
        member _.ContextualPrecedenceToken = cpToken
    interface Production<'T>

module internal ProductionUtils =
    let getBaseInterface (prod: Production<_>) =
        match prod with
        | :? AbstractProduction as x -> x
        | _ ->
            NotSupportedException "Using a custom implementation of the Production<T> interface is not allowed."
            |> raise

/// <summary>The base, untyped interface of <see cref="Nonterminal{T}"/>.</summary>
/// <seealso cref="Nonterminal{T}"/>
type internal AbstractNonterminal =
    inherit DesigntimeFarkle
    /// Gets the productions of the nonterminal.
    /// After this call they cannot be changed by any means.
    abstract FreezeAndGetProductions: unit -> AbstractProduction list

/// <summary>A nonterminal symbol. It is made of <see cref="Production{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this nonterminal generates.
/// All productions of a nonterminal have the same type parameter.</typeparam>
/// <remarks>User code must not inherit from this class,
/// or an exception might be thrown.</remarks>
[<AbstractClass>]
type Nonterminal<[<Nullable(2uy)>] 'T> internal(name, metadata) =
    do nullCheck (nameof name) name
    /// The nonterminal's name.
    member _.Name = name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must only be called once, and before
    /// building a designtime Farkle containing this nonterminal.
    /// Subsequent calls, and these after building are ignored.</remarks>
    abstract SetProductions: firstProd: Production<'T> * [<ParamArray>] prods: Production<'T> [] -> unit
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = metadata
    interface DesigntimeFarkle<'T>

[<Sealed>]
type internal NonterminalReal<'T> internal(name) =
    inherit Nonterminal<'T>(name, GrammarMetadata.Default)

    let mutable latch = Latch.Create false
    let mutable productions = []

    override _.SetProductions(firstProd, prods) =
        nullCheck (nameof firstProd) firstProd
        nullCheck (nameof prods) prods
        for i = 0 to prods.Length - 1 do
            if (obj.ReferenceEquals(prods[i], null)) then
                sprintf "prods[%d]" i |> nullArg
        if latch.TrySet() then
            productions <-
                prods
                |> Seq.map ProductionUtils.getBaseInterface
                |> List.ofSeq
                |> (fun prods -> (ProductionUtils.getBaseInterface firstProd) :: prods)
    interface AbstractNonterminal with
        // If they are already set, nothing will happen.
        // If they haven't been set, they will be permanently
        // set to a broken state.
        member _.FreezeAndGetProductions() =
            latch.Set()
            productions
    interface IExposedAsDesigntimeFarkleChild with
        member x.WithMetadataSameType name metadata =
            NonterminalWrapper<'T>(name, metadata, x) :> _

and [<Sealed>] private NonterminalWrapper<'T> internal(name, metadata, nontReal: NonterminalReal<'T>) =
    inherit Nonterminal<'T>(name, metadata)
    override _.SetProductions(firstProd, prods) =
        nontReal.SetProductions(firstProd, prods)
    interface DesigntimeFarkleWrapper with
        member _.InnerDesigntimeFarkle = nontReal :> _
    interface IExposedAsDesigntimeFarkleChild with
        member _.WithMetadataSameType name metadata =
            NonterminalWrapper<'T>(name, metadata, nontReal) :> _
