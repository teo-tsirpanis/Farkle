// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
open System
open System.Collections.Immutable

/// <summary>The base, untyped interface of <see cref="Production{T}"/>.</summary>
/// <seealso cref="Production{T}"/>
// This type's naming differs from the other interfaces, because there is
// an module that must be called `Production` (so that it has a C#-friendly name).
type internal AbstractProduction =
    /// The members of the production.
    abstract Members: DesigntimeFarkle ImmutableArray
    /// The fuser to process the members of this production.
    abstract Fuser: FuserData

/// <summary>A production. Productions are parts of <see cref="Nonterminal{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this production generates.</typeparam>
[<Sealed>]
type Production<'T> internal(members: _ seq, fuser: FuserData) =
    let members = members.ToImmutableArray()
    interface AbstractProduction with
        member _.Members = members
        member _.Fuser = fuser

/// <summary>The base, untyped interface of <see cref="Nonterminal{T}"/>.</summary>
/// <seealso cref="Nonterminal{T}"/>
type internal AbstractNonterminal =
    inherit DesigntimeFarkle
    /// Makes the nonterminal's productions immutable.
    /// This function was introduced to add more determinism
    /// to the limited mutability allowed in nonterminals.
    abstract Freeze: unit -> unit
    /// The productions of the nonterminal.
    abstract Productions: AbstractProduction list

[<NoComparison; ReferenceEquality>]
/// <summary>A nonterminal symbol. It is made of <see cref="Production{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this nonterminal generates.
/// All productions of a nonterminal have the same type parameter.</typeparam>
type Nonterminal<'T> = internal {
    _Name: string
    Productions: SetOnce<AbstractProduction list>
}
with
    /// The nonterminal's name.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must only be called once, and before
    /// building a designtime Farkle containing this one.
    /// Subsequent calls (and these after building) are ignored.</remarks>
    member x.SetProductions(firstProd: Production<'T>, [<ParamArray>] prods: Production<'T> []) =
        prods
        |> Seq.map (fun x -> x :> AbstractProduction)
        |> List.ofSeq
        |> (fun prods -> (firstProd :> AbstractProduction) :: prods)
        |> x.Productions.TrySet
        |> ignore
    interface AbstractNonterminal with
        // If they are already set, nothing will happen.
        // If they haven't been set, they will be permanently
        // set to a broken state.
        member x.Freeze() = x.Productions.TrySet [] |> ignore
        member x.Productions = x.Productions.ValueOrDefault []
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface DesigntimeFarkle<'T>
