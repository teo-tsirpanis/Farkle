// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.Untyped

// The obsolete Nonterminal.CreateUntyped
// calls the obsolete SetProductions.
#nowarn "44"

open Farkle.Builder
open Farkle.Common
open System

[<NoComparison; ReferenceEquality>]
/// <summary>A nonterminal that does not have an associated type.</summary>
/// <remarks>This type is easier to work with, if you want to just
/// define a grammar as you would do in GOLD Parser. Because it only implements
/// the untyped <see cref="Farkle.Builder.DesigntimeFarkle"/> interface,
/// it can only be <c>Append</c>ed but not <c>Extend</c>ed in
/// production builders. Overall, it is a normal designtime Farkle.</remarks>
type Nonterminal = internal {
    _Name: string
    Productions: SetOnce<AbstractProduction list>
}
with
    /// The name of the nonterminal.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must be called exactly once. Subsequent calls are ignored.
    /// It accepts un<c>Finish</c>ed production builders with no significant members.</remarks>
    member x.SetProductions(firstProd: ProductionBuilder, [<ParamArray>] prods: ProductionBuilder []) =
        prods
        |> Seq.cast<AbstractProduction>
        |> List.ofSeq
        |> (fun prods -> firstProd :> AbstractProduction :: prods)
        |> x.Productions.TrySet
        |> ignore
    /// <summary>Creates an untyped <see cref="Nonterminal"/>.
    /// Its productions must be set later.</summary>
    /// <remarks>This function is useful for the creation of recursive productions.</remarks>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member Create(name) =
        nullCheck "name" name
        {
            _Name = name
            Productions = SetOnce<_>.Create()
        }
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions,
    /// declared as production builders.</summary>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member Create(name, firstProd: ProductionBuilder, [<ParamArray>] prods) =
        let nont = Nonterminal.Create name
        nont.SetProductions(firstProd, prods)
        nont :> DesigntimeFarkle
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface AbstractNonterminal with
        member x.Freeze() = x.Productions.TrySet [] |> ignore
        member x.Productions = x.Productions.ValueOrDefault []
