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

type Terminal<'T> = internal {
    Id: Guid
    _Name: string
    Regex: Regex
    Transformer: T<obj> 
}
with
    member x.Name = x._Name
    interface Terminal with
        member x.Id = x.Id
        member x.Regex = x.Regex
        member x.Transformer = x.Transformer
    interface DesigntimeFarkle with
        member x.Name = x.Name
    interface DesigntimeFarkle<'T>

module Terminal =

    [<CompiledName("Create")>]
    let create name (fTransform: T<'T>) regex: Terminal<'T> = {
        Id = Guid.NewGuid()
        _Name = name
        Regex = regex
        Transformer = T(fun pos data -> fTransform.Invoke(pos, data) |> box)
    }

type Nonterminal<'T> = internal {
    Id: Guid
    Name: string
    Productions: SetOnce<Production list>
}
with
    static member Create name = {
        Id = Guid.NewGuid()
        Name = name
        Productions = SetOnce<_>.Create()
    }
    member x.SetProductions([<ParamArray>] prods: Production<'T> []) =
        prods
        |> Seq.map (fun x -> x :> Production)
        |> List.ofSeq
        |> x.Productions.TrySet
        |> ignore
    interface Nonterminal with
        member x.Id = x.Id
        // member x.Name = x.Name
        member x.Productions = x.Productions.ValueOrDefault []
    interface DesigntimeFarkle with
        member x.Name = x.Name
    interface DesigntimeFarkle<'T>

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

type AbstractProductionBuilder<'T> =
    abstract Append: DesigntimeFarkle -> 'T

[<Sealed>]
type ProductionBuilder(members) =
    interface AbstractProductionBuilder<ProductionBuilder> with
        member __.Append(sym) = ProductionBuilder(Symbol.append members sym)
    member __.Extend(df: DesigntimeFarkle<'T1>) = ProductionBuilder<'T1>(ImmutableList.add members (Symbol.specialize df), members.Count)
    member __.FinishRaw(fFuseRaw: _ -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fFuseRaw >> box
    }
    member x.Finish(fFuseThunk) = x.FinishRaw(fun _ -> fFuseThunk ())
    member x.Finish(fConstant) = x.FinishRaw(fun _ -> fConstant)

[<Sealed>]
type ProductionBuilder<'T1>(members, idx1) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1>> with
        member __.Append(sym) = ProductionBuilder<_>(Symbol.append members sym, idx1)
    member __.Extend(df: DesigntimeFarkle<'T2>) = ProductionBuilder<'T1, 'T2>(ImmutableList.add members (Symbol.specialize df), idx1, members.Count)
    member __.Finish(f: 'T1 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) |> box
    }

[<Sealed>]
type ProductionBuilder<'T1, 'T2>(members, idx1, idx2) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1, 'T2>> with
        member __.Append(sym) = ProductionBuilder<_, _>(Symbol.append members sym, idx1, idx2)
    member __.Extend(df: DesigntimeFarkle<'T3>) = ProductionBuilder<'T1, 'T2, 'T3>(ImmutableList.add members (Symbol.specialize df), idx1, idx2, members.Count)
    member __.Finish(f: 'T1 -> 'T2 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) |> box
    }

[<Sealed>]
type ProductionBuilder<'T1, 'T2, 'T3>(members, idx1, idx2, idx3) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1, 'T2, 'T3>> with
        member __.Append(sym) = ProductionBuilder<_, _, _>(Symbol.append members sym, idx1, idx2, idx3)
    member __.Finish(f: 'T1 -> 'T2 -> 'T3 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) (arr.[idx3] :?> _) |> box
    }

module Production =

    [<CompiledName("Empty")>]
    let empty = ProductionBuilder(ImmutableList.Empty)

    [<CompiledName("Append")>]
    let (!%) df =
        df
        |> Symbol.specialize
        |> ImmutableList.Empty.Add
        |> ProductionBuilder

    [<CompiledName("Extend")>]
    let (!@) (df: DesigntimeFarkle<'T>) = empty.Extend(df)

module DesigntimeFarkleOperators =

    let (||=) name parts =
        let nont = Nonterminal.Create name
        nont.SetProductions(Array.ofSeq parts)
        nont

    let inline (@=>) (pb: ^TBuilder when ^TBuilder : (member Finish : ^TFunction -> Production<_>)) f =
        (^TBuilder : (member Finish: ^TFunction -> Production<_>) (pb, f))

    let inline (@=%) (pb: ProductionBuilder) (x: 'T) = pb.Finish(x)

    // https://github.com/ionide/ionide-vscode-fsharp/issues/1203
    let inline op_DotGreaterGreater (pb: #AbstractProductionBuilder<_>) df = pb.Append df

    let inline op_DotGreaterGreaterDot (rb: ^TBuilder when ^TBuilder : (member Extend : DesigntimeFarkle<_> -> ^TBuilderResult)) df =
        (^TBuilder : (member Extend: DesigntimeFarkle<_> -> ^TBuilderResult) (rb, df))
