// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace rec Farkle.Builder

open Farkle
open Farkle.Collections
open System
open System.Collections.Immutable

type T<'T> = delegate of ReadOnlySpan<char> * Position -> 'T

type Terminal =
    abstract Id: Guid
    abstract Name: string
    abstract Regex: Regex
    abstract Transformer: T<obj>

type Nonterminal =
    abstract Id: Guid
    abstract Name: String
    abstract Parts: Production list

type Production =
    abstract Members: Symbol ImmutableArray
    abstract Fuse: obj [] -> obj

type Symbol = Choice<Terminal, Nonterminal>

type Terminal<'T> = internal {
    Id: Guid
    Name: string
    Regex: Regex
    Transformer: T<obj> 
}
with
    interface Terminal with
        member x.Id = x.Id
        member x.Name = x.Name
        member x.Regex = x.Regex
        member x.Transformer = x.Transformer

type Nonterminal<'T> = internal {
    Id: Guid
    Name: string
    mutable Parts: Production list
}
with
    interface Nonterminal with
        member x.Id = x.Id
        member x.Name = x.Name
        member x.Parts = x.Parts

type Production<'T> = internal {
    Members: Symbol ImmutableArray
    Fuse: obj [] -> obj
}
with
    interface Production with
        member x.Members = x.Members
        member x.Fuse arguments = x.Fuse arguments

type Symbol<'T> = Choice<Terminal<'T>, Nonterminal<'T>>

module Symbol =
    let generalize (x: Symbol<_>): Symbol =
        match x with
        | Choice1Of2 x -> x :> Terminal |> Choice1Of2
        | Choice2Of2 x -> x :> Nonterminal |> Choice2Of2

type AbstractProductionBuilder<'T> =
    abstract DoAppend: Symbol -> 'T

type ProductionBuilder(members) =
    interface AbstractProductionBuilder<ProductionBuilder> with
        member __.DoAppend(sym) = ProductionBuilder(ImmutableList.add members sym)
    member __.Extend(sym: Symbol<'T1>) = ProductionBuilder<'T1>(ImmutableList.add members (Symbol.generalize sym), members.Count)
    member __.FinishRaw(fFuseRaw: _ -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fFuseRaw >> box
    }
    member x.Finish(fFuseThunk) = x.FinishRaw(fun _ -> fFuseThunk ())
    member x.Finish(fConstant) = x.FinishRaw(fun _ -> fConstant)

type ProductionBuilder<'T1>(members, idx1) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1>> with
        member __.DoAppend(sym) = ProductionBuilder<_>(ImmutableList.add members sym, idx1)
    member __.Extend(sym: Symbol<'T2>) = ProductionBuilder<'T1, 'T2>(ImmutableList.add members (Symbol.generalize sym), idx1, members.Count)
    member __.Finish(f: 'T1 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) |> box
    }

type ProductionBuilder<'T1, 'T2>(members, idx1, idx2) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1, 'T2>> with
        member __.DoAppend(sym) = ProductionBuilder<_, _>(ImmutableList.add members sym, idx1, idx2)
    member __.Extend(sym: Symbol<'T3>) = ProductionBuilder<'T1, 'T2, 'T3>(ImmutableList.add members (Symbol.generalize sym), idx1, idx2, members.Count)
    member __.Finish(f: 'T1 -> 'T2 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) |> box
    }

type ProductionBuilder<'T1, 'T2, 'T3>(members, idx1, idx2, idx3) =
    interface AbstractProductionBuilder<ProductionBuilder<'T1, 'T2, 'T3>> with
        member __.DoAppend(sym) = ProductionBuilder<_, _, _>(ImmutableList.add members sym, idx1, idx2, idx3)
    member __.Finish(f: 'T1 -> 'T2 -> 'T3 -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fun arr -> f (arr.[idx1] :?> _) (arr.[idx2] :?> _) (arr.[idx3] :?> _) |> box
    }
