// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace rec Farkle.Builder

open Farkle
open Farkle.Common
open Farkle.Collections
open System
open System.Collections.Immutable

[<CompiledName("TerminalCallback`1")>]
/// <summary>A delegate that accepts the position and data of a terminal, and transforms them into an arbitrary object.</summary>
/// <remarks>
///     <para>In F#, this type is named <c>T</c> - from "Terminal" and was shortened to avoid clutter in user code.</para>
///     <para>A .NET delegate was used because <see cref="ReadOnlySpan{Char}"/>s are incompatible with F# functions</para>
/// </remarks>
type T<'T> = delegate of Position * ReadOnlySpan<char> -> 'T

/// <summary>The base, untyped interface of <see cref="DesigntimeFarkle{T}"/>.</summary>
/// <remarks>User code must not implement this interface, or an error may be raised.</remarks>
/// <seealso cref="DesigntimeFarkle{T}"/>
type DesigntimeFarkle =
    abstract Name: string

[<CompiledName("AbstractTerminal")>]
/// <summary>The base, untyped interface of <see cref="Terminal{T}"/>.</summary>
/// <seealso cref="Terminal{T}"/>
type Terminal =
    inherit DesigntimeFarkle
    abstract Id: Guid
    abstract Regex: Regex
    abstract Transformer: T<obj>

[<CompiledName("AbstractNonterminal")>]
/// <summary>The base, untyped interface of <see cref="Nonterminal{T}"/>.</summary>
/// <seealso cref="Nonterminal{T}"/>
type Nonterminal =
    inherit DesigntimeFarkle
    abstract Id: Guid
    abstract Productions: Production list

[<CompiledName("AbstractProduction")>]
/// <summary>The base, untyped interface of <see cref="Production{T}"/>.</summary>
/// <seealso cref="Production{T}"/>
type Production =
    abstract Members: Symbol ImmutableArray
    abstract Fuse: obj [] -> obj

type Symbol = Choice<Terminal, Nonterminal>

/// <summary>An object representing a grammar created by Farkle.Builder.
/// It can be converted to <see cref="RuntimeFarkle{T}"/>.</summary>
/// <remarks>
///     <para>This interface is implemented by <see cref="Terminal{T}"/> and <see cref="Nonterminal{T}"/>.</para>
///     <para>User code must not implement this interface, or an error may be raised.</para>
/// </remarks>
/// <typeparam name="T">The type of the objects this grammar generates.</typeparam>
type DesigntimeFarkle<'T> = 
    inherit DesigntimeFarkle

/// <summary>A terminal symbol.</summary>
/// <typeparam name="T">The type of objects this terminal generates.</typeparam>
type Terminal<'T> = internal {
    Id: Guid
    _Name: string
    Regex: Regex
    Transformer: T<obj> 
}
with
    /// The terminal's name.
    member x.Name = x._Name
    interface Terminal with
        member x.Id = x.Id
        member x.Regex = x.Regex
        member x.Transformer = x.Transformer
    interface DesigntimeFarkle with
        member x.Name = x._Name
    interface DesigntimeFarkle<'T>

/// Functions to create `Terminal`s.
module Terminal =

    [<CompiledName("Create")>]
    /// <summary>Creates a <see cref="Terminal{T}"/>.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="fTransform">The function that transforms the terminal's position and data to <c>T</c>.</param>
    /// <param name="regex">The terminal's corresponding regular expression.</param>
    let create name (fTransform: T<'T>) regex: Terminal<'T> = {
        Id = Guid.NewGuid()
        _Name = name
        Regex = regex
        Transformer = T(fun pos data -> fTransform.Invoke(pos, data) |> box)
    }

/// <summary>A nonterminal symbol. It is made of <see cref="Production{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this nonterminal generates. All productions of a nonterminal have the same type parameter.</typeparam>
type Nonterminal<'T> = internal {
    Id: Guid
    _Name: string
    Productions: SetOnce<Production list>
}
with
    /// <summary>Creates a <see cref="Nonterminal{T}"/> whose productions can be later set.</summary>
    /// <remarks>
    ///     <para>If they are not set, it is assumed that the nonterminal consists only of the empty string.</para>
    ///     <para>This method exists to help write recursive grammars.</para>
    /// </remarks>
    /// <param name="name">The nonterminal's name.</param>
    static member Create name = {
        Id = Guid.NewGuid()
        _Name = name
        Productions = SetOnce<_>.Create()
    }
    /// The nonterminal's name.
    member x.Name = x._Name
    /// <summary>Sets the nonterminal's productions.</summary>
    /// <remarks>This method must only be called once. Subsequent calls are ignored.</remarks>
    member x.SetProductions([<ParamArray>] prods: Production<'T> []) =
        prods
        |> Seq.map (fun x -> x :> Production)
        |> List.ofSeq
        |> x.Productions.TrySet
        |> ignore
    /// <summary>Creates a <see cref="Nonterminal{T}"/> with the given name and productions.</summary>
    static member Create (name, [<ParamArray>] productions) =
        let prod = Nonterminal.Create name
        prod.SetProductions(productions)
        prod
    interface Nonterminal with
        member x.Id = x.Id
        member x.Productions = x.Productions.ValueOrDefault []
    interface DesigntimeFarkle with
        member x.Name = x._Name
    interface DesigntimeFarkle<'T>

/// <summary>A production. Productions are parts of <see cref="Nonterminal{T}"/>s.</summary>
/// <typeparam name="T">The type of the objects this production generates.</typeparam>
type Production<'T> = internal {
    Members: Symbol ImmutableArray
    Fuse: obj [] -> obj
}
with
    interface Production with
        member x.Members = x.Members
        member x.Fuse arguments = x.Fuse arguments

module private Symbol =
    let specialize (x: DesigntimeFarkle): Symbol =
        match x with
        | :? Terminal as term -> Choice1Of2 term
        | :? Nonterminal as nont -> Choice2Of2 nont
        | _ -> failwith "Using a custom derivative of the DesigntimeFarkle interface is not allowed."
    let append xs df = ImmutableList.add xs (specialize df)

[<AbstractClass>]
/// <summary>The base, untyped class of the production builders.</summary>
/// <remarks>
///     <para>A production builder is an object that helps to fluently construct <see cref="Production{T}"/>s
///     by aggregating the types of its significant parts. The types of the production's
///     significant parts are indicated by the type parameters. For example, a <c>ProductionBuilder</c> has no
///     significant parts, and a <c>ProductionBuilder&lt;int, string&gt;</c> has two significant parts; an integer
///     and a string.</para>
///     <para>Production builders have three basic methods.</para>
///     <para><c>Append</c> returns a production builder with the same significant parts,
///     but the given <see cref="DesigntimeFarkle"/> at the end of it.</para>
///     <para><c>Extend</c> returns a production builder with one more significant part,
///     whose type is determined by the given <see cref="DesigntimeFarkle{T}"/> that will
///     be appended to it.</para>
///     <para><c>Finish</c> accepts a function that converts all the builder's significant parts
///     into the eventual type of the returned <see cref="Production{T}"/>.</para>
/// </remarks>
/// <typeparam name="T">The type of the concrete production builder. Used so that
/// <see cref="AbstractProductionBuilder{TBuilder}.Append"/> can return the correct production builder type</typeparam>
type AbstractProductionBuilder<'TBuilder>() =
    /// <seealso cref="AbstractProductionBuilder{TBuilder}"/>
    abstract Append: DesigntimeFarkle -> 'TBuilder

[<Sealed>]
type ProductionBuilder(members) =
    inherit AbstractProductionBuilder<ProductionBuilder>()
    override __.Append(sym) = ProductionBuilder(Symbol.append members sym)
    member __.Extend(df: DesigntimeFarkle<'T1>) = ProductionBuilder<'T1>(ImmutableList.add members (Symbol.specialize df), members.Count)
    member x.Finish(fFuseThunk) = x.FinishRaw(fun _ -> fFuseThunk ())
    /// <summary>Like <c>Finish</c>, but the given function accepts
    /// an array of all the production's parts as objects.</summary>
    /// <remarks>
    ///     <para>This method is intended to be used when finishing a production with many significant parts.</para>
    ///     <para>Do not rely on the array's length; it can be larger than the number of parts of the production.</para>
    /// </remarks>
    member __.FinishRaw(fFuseRaw: _ -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fFuseRaw >> box
    }
    /// <summary>Creates a <see cref="Production{T}"/> that always returns a constant value.</summary>
    member x.FinishConstant(v) = x.FinishRaw(fun _ -> v)

[<Sealed>]
type ProductionBuilder<'T1>(members, idx1) =
    inherit AbstractProductionBuilder<ProductionBuilder<'T1>>()
    override __.Append(sym) = ProductionBuilder<_>(Symbol.append members sym, idx1)
    member __.Extend(df: DesigntimeFarkle<'T2>) = ProductionBuilder<'T1, 'T2>(ImmutableList.add members (Symbol.specialize df), idx1, members.Count)
    member __.Finish(f: 'T1 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) |> box
    }

[<Sealed>]
type ProductionBuilder<'T1, 'T2>(members, idx1, idx2) =
    inherit AbstractProductionBuilder<ProductionBuilder<'T1, 'T2>>()
    override __.Append(sym) = ProductionBuilder<_, _>(Symbol.append members sym, idx1, idx2)
    member __.Extend(df: DesigntimeFarkle<'T3>) = ProductionBuilder<'T1, 'T2, 'T3>(ImmutableList.add members (Symbol.specialize df), idx1, idx2, members.Count)
    member __.Finish(f: 'T1 -> 'T2 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) |> box
    }

[<Sealed>]
type ProductionBuilder<'T1, 'T2, 'T3>(members, idx1, idx2, idx3) =
    inherit AbstractProductionBuilder<ProductionBuilder<'T1, 'T2, 'T3>>()
    override __.Append(sym) = ProductionBuilder<_, _, _>(Symbol.append members sym, idx1, idx2, idx3)
    member __.Finish(f: 'T1 -> 'T2 -> 'T3 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) (arr.[idx3] :?> _) |> box
    }

[<AutoOpen>]
/// Some helper functions to create production builders.
/// This module is also intended to be used from C#.
module Production =

    [<CompiledName("Empty")>]
    /// A production builder with no parts.
    let empty = ProductionBuilder(ImmutableList.Empty)

    [<CompiledName("Append")>]
    /// Creates a production builder with one non-significant `DesigntimeFarkle`.
    /// This function is useful to start building a `Production`.
    let (!%) df =
        df
        |> Symbol.specialize
        |> ImmutableList.Empty.Add
        |> ProductionBuilder

    /// Creates a production builder with one significant `DesigntimeFarkle<'T>`.
    /// This function is useful to start building a `Production`.
    [<CompiledName("Extend")>]
    let (!@) (df: DesigntimeFarkle<'T>) = empty.Extend(df)

[<AutoOpen>]
/// F# operators to easily work with productions and their builders.
module DesigntimeFarkleOperators =

    /// Creates a `NonTerminal<'T>` with the given name and parts.
    let inline (||=) name parts = Nonterminal.Create(name, Array.ofSeq parts)

    /// The `Append` method of production builders as an operator.
    // https://github.com/ionide/ionide-vscode-fsharp/issues/1203
    let inline op_DotGreaterGreater (pb: #AbstractProductionBuilder<_>) df = pb.Append df

    /// The `Extend` method of production builders as an operator.
    let inline op_DotGreaterGreaterDot (rb: ^TBuilder when ^TBuilder : (member Extend : DesigntimeFarkle<_> -> ^TBuilderResult)) df =
        (^TBuilder : (member Extend: DesigntimeFarkle<_> -> ^TBuilderResult) (rb, df))

    /// The `Finish` method of production builders as an operator.
    let inline (@=>) (pb: ^TBuilder when ^TBuilder : (member Finish : ^TFunction -> Production<'T>)) f =
        (^TBuilder : (member Finish: ^TFunction -> Production<'T>) (pb, f))

    /// `ProductionBuilder.FinishConstant` as an operator.
    let inline (@=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)
