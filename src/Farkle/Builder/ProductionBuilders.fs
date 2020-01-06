// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open System
open System.Collections.Immutable

[<Sealed>]
/// <summary>The base, untyped class of the production builders.</summary>
/// <remarks>
///     <para>A production builder is an object that helps to fluently construct <see cref="Production{T}"/>s
///     by aggregating the types of its significant members. The types of the production's
///     significant members are indicated by the type parameters. For example, a <c>ProductionBuilder</c> has no
///     significant members, and a <c>ProductionBuilder&lt;int, string&gt;</c> has two significant members: an integer
///     and a string.</para>
///     <para>Production builders have three basic methods.</para>
///     <para><c>Append</c> returns a production builder with the same significant members,
///     but the given <see cref="DesigntimeFarkle"/> at the end of it. Alternatively, it can
///     accept a string which will be appended to the production as a literal.</para>
///     <para><c>Extend</c> returns a production builder with one more significant member,
///     whose type is determined by the given <see cref="DesigntimeFarkle{T}"/> that will
///     be appended to it.</para>
///     <para><c>Finish</c> accepts a function that converts all the builder's significant members
///     into the eventual type of the returned <see cref="Production{T}"/>. It comes in two editions.
///     One that takes an F# function and another one that takes a delegate.</para>
/// </remarks>
/// <typeparam name="T">The type of the concrete production builder. Used so that
/// <see cref="AbstractProductionBuilder{TBuilder}.Append"/>
/// can return the correct production builder type</typeparam>
type ProductionBuilder internal(members) =
    /// A production builder with no members.
    static member Empty = ProductionBuilder(ImmutableList.Empty)
    member __.Append(sym) = ProductionBuilder(Symbol.append members sym)
    member x.Append(lit) = x.Append(Literal lit)
    member __.Extend(df: DesigntimeFarkle<'T1>) =
        ProductionBuilder<'T1>(Symbol.append members df, members.Count)
    /// <summary>Like <c>Finish</c>, but the given function accepts
    /// an array of all the production's members as objects.</summary>
    /// <remarks>
    ///     <para>This method is intended to be used when finishing
    ///     a production with many significant members.</para>
    ///     <para>Do not rely on the array's length; it can be larger
    ///     than the number of members of the production.</para>
    /// </remarks>
    member __.FinishRaw(fFuseRaw: _ -> 'TOutput) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse = fFuseRaw >> box
    }
    member x.FinishFSharp fFuseThunk = x.FinishRaw(fun _ -> fFuseThunk())
    member x.Finish(f: Func<_>) = x.FinishFSharp(FuncConvert.FromFunc(f))
    /// Creates an untyped production. This function is internally
    /// used by the untyped nonterminals API.
    member internal __.FinishUntyped() = {new AbstractProduction with
        member __.Members = members.ToImmutableArray()
        member __.Fuse = (fun _ -> null)}
    /// <summary>Creates a <see cref="Production{T}"/> tha
    ///  always returns a constant value.</summary>
    member x.FinishConstant(v) = x.FinishRaw(fun _ -> v)
