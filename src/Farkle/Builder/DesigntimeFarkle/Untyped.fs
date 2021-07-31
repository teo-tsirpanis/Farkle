// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.Untyped

open Farkle.Builder
open Farkle.Common
open System

/// <summary>A nonterminal that does not have an associated return type.</summary>
/// <remarks><para>This type is easier to work with if you want to just
/// define a grammar as you would do in GOLD Parser. Because it only implements
/// the untyped <see cref="Farkle.Builder.DesigntimeFarkle"/> interface,
/// it can only be <c>Append</c>ed but not <c>Extend</c>ed in
/// production builders. Overall, it is a normal designtime Farkle.</para>
/// <para>User code must not inherit from this class,
/// or an exception might be thrown.</para></remarks>
[<AbstractClass>]
type Nonterminal internal(name, metadata) =
    do nullCheck (nameof name) name
    /// The nonterminal's name.
    member _.Name = name
    /// <remarks>This method must only be called once, and before
    /// building a designtime Farkle containing this nonterminal.
    /// Subsequent calls, and these after building are ignored.
    /// It accepts un<c>Finish</c>ed production builders with no significant members.</remarks>
    abstract SetProductions: firstProd: ProductionBuilder * [<ParamArray>] prods: ProductionBuilder [] -> unit
    /// <summary>Creates an untyped <see cref="Nonterminal"/>.
    /// Its productions must be set later.</summary>
    /// <remarks>This function is useful for the creation of recursive productions.</remarks>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member Create(name) =
        NonterminalReal(name) :> Nonterminal
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions,
    /// declared as production builders.</summary>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member Create(name, firstProd: ProductionBuilder, [<ParamArray>] prods) =
        let nont = NonterminalReal name
        nont.SetProductions(firstProd, prods)
        nont :> DesigntimeFarkle
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = metadata

and [<Sealed>] internal NonterminalReal(name: string) =
    inherit Nonterminal(name, GrammarMetadata.Default)

    let mutable latch = Latch.Create false
    let mutable productions = []

    override _.SetProductions(firstProd: ProductionBuilder, [<ParamArray>] prods: ProductionBuilder []) =
        if latch.TrySet() then
            productions <-
                prods
                |> Seq.cast<AbstractProduction>
                |> List.ofSeq
                |> (fun prods -> firstProd :> AbstractProduction :: prods)
    interface AbstractNonterminal with
        member _.FreezeAndGetProductions() =
            latch.Set()
            productions
    interface IExposedAsDesigntimeFarkleChild with
        member x.WithMetadataSameType name metadata =
            NonterminalWrapper(name, metadata, x) :> _

and [<Sealed>] private NonterminalWrapper internal(name, metadata, nontReal: NonterminalReal) =
    inherit Nonterminal(name, metadata)
    override _.SetProductions(firstProd, prods) =
        nontReal.SetProductions(firstProd, prods)
    interface DesigntimeFarkleWrapper with
        member _.InnerDesigntimeFarkle = nontReal :> _
    interface IExposedAsDesigntimeFarkleChild with
        member _.WithMetadataSameType name metadata =
            NonterminalWrapper(name, metadata, nontReal) :> _
