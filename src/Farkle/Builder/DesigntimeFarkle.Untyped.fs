// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.Untyped

open Farkle.Builder
open Farkle.Common
open System

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
    /// un<c>Finish</c>ed production builders and is intended to be
    /// used by F# because of the more terse API for creating them.</remarks>
    member x.SetProductions(firstProd: ProductionBuilder, [<ParamArray>] prods: ProductionBuilder []) =
        prods
        |> Seq.map (fun p -> p.FinishUntyped())
        |> List.ofSeq
        |> (fun prods -> firstProd.FinishUntyped() :: prods)
        |> x.Productions.TrySet
        |> ignore
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must be called exactly once. It accepts
    /// a variable amount of object sequences. Each object should be
    /// either a <see cref="DesigntimeFarkle"/> or a string. In the
    /// latter case, they will be used as literals. This method is intended
    /// to be used by C# because</remarks>
    /// <seealso cref="Nonterminal.CreateUntyped(System.String)"/>
    member x.SetProductions(firstProd: obj seq, [<ParamArray>] prods: obj seq []) =
        let fSpecialize (x: obj) =
            match x with
            | :? DesigntimeFarkle as df -> df
            | :? string as str -> literal str
            | x -> failwith "Only designtime Farkles and strings are \
allowed in an untyped nonterminal. You provided a %O" <| x.GetType()
        let makePB xs =
            (empty, xs) ||> Seq.fold (fun pb x -> pb.Append(fSpecialize x))
        x.SetProductions(makePB firstProd, Array.map makePB prods)
    /// <summary>Creates an untyped <see cref="Nonterminal"/>.
    /// Its productions must be set later.</summary>
    /// <remarks>This function is useful for the creation
    /// of recursive or cyclical productions.</remarks>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member CreateUntyped(name) = {
        _Name = name
        Productions = SetOnce<_>.Create()
    }
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions,
    /// declared as production builders.</summary>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member CreateUntyped(name, firstProd: ProductionBuilder, [<ParamArray>] prods) =
        let nont = Nonterminal.CreateUntyped name
        nont.SetProductions(firstProd, prods)
        nont :> DesigntimeFarkle
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions,
    /// declared as sequences of objects.</summary>
    /// <seealso cref="Nonterminal.SetProductions"/>
    static member CreateUntyped(name, firstProd: obj seq, [<ParamArray>] prods) =
        let nont = Nonterminal.CreateUntyped name
        nont.SetProductions(firstProd, prods)
        nont :> DesigntimeFarkle
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface AbstractNonterminal with
        member x.Productions = x.Productions.ValueOrDefault []

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleUntypedOperators")>]
/// F# operators to easily work with untyped `DesigntimeFarkle`s.
module DesigntimeFarkleUntypedOperators =

    /// Creates an untyped terminal from the given name and specified by the given `Regex`.
    let inline terminal name regex = Terminal.Create(name, regex)

    /// Creates an untyped `Nonterminal` whose productions must be set later.
    let inline nonterminal name = Nonterminal.CreateUntyped name

    /// Creates an untyped `DesigntimeFarkle` that represents
    /// a nonterminal with the given name and productions.
    let (||=) name members =
        let nont = nonterminal name
        match members with
        | [] -> ()
        | (x: ProductionBuilder) :: xs -> nont.SetProductions(x, Array.ofList xs)
        nont :> DesigntimeFarkle
