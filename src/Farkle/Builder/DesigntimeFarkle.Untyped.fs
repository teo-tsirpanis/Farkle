// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.Untyped

open Farkle.Builder
open Farkle.Common
open System
open System.Collections.Immutable

/// <summary>A nonterminal that does not have an associated type.</summary>
/// <remarks>This type is easier to work with, if you want to just
/// define a grammar as you would do in GOLD Parser. Because it only implements
/// the untyped <see cref="Farkle.Builder.DesigntimeFarkle"/> interface,
/// it can still be <c>Append</c>ed, but not <c>Extend</c>ed in
/// typed production builders. Overall, it is a normal designtime Farkle.</remarks>
type Nonterminal = internal {
    _Name: string
    Productions: SetOnce<AbstractProduction list>
}
with
    /// The name of the nonterminal.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must be called exactly once. It accepts
    /// a variable amount of object sequences. Each object should be
    /// either a <see cref="DesigntimeFarkle"/> or a string. In the
    /// latter case, they will be used as literals.</remarks>
    /// <seealso cref="Nonterminal.CreateUntyped(System.String)"/>
    member x.SetProductions([<ParamArray>] prods: obj seq []) =
        let fSpecialize (x: obj) =
            match x with
            | :? DesigntimeFarkle as df -> df
            | :? string as str -> literal str
            | x -> failwith "Only designtime Farkles and strings are \
allowed in an untyped nonterminal. You provided a %O" <| x.GetType()
            |> Symbol.specialize
        let makeProduction xs =
            let members = Seq.map fSpecialize xs |> ImmutableArray.CreateRange
            {new AbstractProduction with
                member __.Members = members
                member __.Fuse = (fun _ -> null)}
        prods
        |> Seq.map makeProduction
        |> List.ofSeq
        |> x.Productions.TrySet
        |> ignore
    /// <summary>Creates an untyped <see cref="Nonterminal"/>.
    /// Its productions can be set later.</summary>
    /// <remarks>This function is useful for the creation
    /// of recursive or cyclical productions.</remarks>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member CreateUntyped(name) = {
        _Name = name
        Productions = SetOnce<_>.Create()
    }
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions.</summary>
    static member CreateUntyped(name, [<ParamArray>] prods) =
        let nont = Nonterminal.CreateUntyped name
        nont.SetProductions(prods)
        nont :> DesigntimeFarkle
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface AbstractNonterminal with
        member x.Productions = x.Productions.ValueOrDefault []

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleUntypedOperators")>]
/// F# operators to easily work with untyped `DesigntimeFarkle`s.
module DesigntimeFarkleUntypedOperators =
    let terminal name regex = terminal name tNull regex :> DesigntimeFarkle

    let inline literal str = literal str

    let inline nonterminal name = Nonterminal.CreateUntyped name

    let inline (||=) name members =
        Nonterminal.CreateUntyped(name, Array.ofSeq members)
