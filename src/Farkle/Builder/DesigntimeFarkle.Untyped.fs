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
    [<Obsolete("Create production builders from an array of objects.")>]
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must be called exactly once. It accepts
    /// a variable amount of object sequences. Each object should be
    /// either a <see cref="DesigntimeFarkle"/> or a string. In the
    /// latter case, they will be used as literals.</remarks>
    /// <seealso cref="Nonterminal.CreateUntyped(System.String)"/>
    member x.SetProductions(firstProd: obj seq, [<ParamArray>] prods: obj seq []) =
        x.SetProductions(Array.ofSeq firstProd |> ProductionBuilder,
            Array.map (Array.ofSeq >> ProductionBuilder) prods)
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
    /// <summary>Creates an untyped <see cref="DesigntimeFarkle"/>
    /// from a nonterminal with the given name and productions,
    /// declared as sequences of objects.</summary>
    /// <seealso cref="Nonterminal.SetProductions"/>
    [<Obsolete("Create production builders from an array of objects.")>]
    static member CreateUntyped(name, firstProd: obj seq, [<ParamArray>] prods) =
        let nont = Nonterminal.Create name
        nont.SetProductions(firstProd, prods)
        nont :> DesigntimeFarkle
    [<Obsolete("Use Create.")>]
    static member CreateUntyped(name) = Nonterminal.Create(name)
    [<Obsolete("Use Create.")>]
    static member CreateUntyped(name, firstProd: ProductionBuilder, [<ParamArray>] prods) =
        Nonterminal.Create(name, firstProd, prods)
    interface DesigntimeFarkle with
        member x.Name = x._Name
        member __.Metadata = GrammarMetadata.Default
    interface AbstractNonterminal with
        member x.Freeze() = x.Productions.TrySet [] |> ignore
        member x.Productions = x.Productions.ValueOrDefault []

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleUntypedOperators")>]
/// F# operators to easily work with untyped `DesigntimeFarkle`s.
module DesigntimeFarkleUntypedOperators =

    [<Obsolete("Open Farkle.Builder and use terminalU.")>]
    /// Creates an untyped terminal from the given name and specified by the given `Regex`.
    let inline terminal name regex = Terminal.Create(name, regex)

    [<Obsolete("Open Farkle.Builder and use nonterminalU.")>]
    /// Creates an untyped `Nonterminal` whose productions must be set later.
    let inline nonterminal name = Nonterminal.Create name

    [<Obsolete("Open Farkle.Builder and use the |||= operator.")>]
    /// Creates an untyped `DesigntimeFarkle` that represents
    /// a nonterminal with the given name and productions.
    let (||=) name members =
        match members with
        | [] -> Nonterminal.Create name :> DesigntimeFarkle
        | (x: ProductionBuilder) :: xs -> Nonterminal.Create(name, x, Array.ofList xs)
