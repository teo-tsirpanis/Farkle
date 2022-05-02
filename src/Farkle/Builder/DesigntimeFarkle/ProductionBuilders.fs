// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Builder.ProductionBuilders
open Farkle.Common
open System
open System.ComponentModel
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// <summary>The base, untyped class of the production builders.</summary>
/// <remarks>
///     <para>A production builder is an object that helps to fluently construct <see cref="Production{T}"/>s
///     by aggregating the types of its significant members. The types of the production's
///     significant members are indicated by the type parameters. For example, a <c>ProductionBuilder</c> has no
///     significant members, and a <c>ProductionBuilder{int, string}</c> has two significant members: an integer
///     and a string.</para>
///     <para>Production builders have these common methods.</para>
///     <para><c>Append</c> returns a production builder with the same significant members,
///     but the given <see cref="DesigntimeFarkle"/> at the end of it. Alternatively, it can
///     accept a string which will be appended to the production as a literal.</para>
///     <para><c>Extend</c> returns a production builder with one more significant member,
///     whose type is determined by the given <see cref="DesigntimeFarkle{T}"/> that will
///     be appended to it.</para>
///     <para><c>Finish</c> accepts a function that converts all the builder's significant members
///     into the eventual type of the returned <see cref="Production{T}"/>. It comes in two editions:
///     one that takes an F# function and another one that takes a delegate.</para>
///     <para><c>WithPrecedence</c> accepts a unique object which will represent the production in
///     operator scopes, providing contextual precedence. An overload of this method accepts
///     a reference to a variable that will hold the object, which will be created by Farkle.
///     It allows C# programmers to use <c>out var</c> instead of creating their own object.</para>
/// </remarks>
[<Sealed>]
type ProductionBuilder internal(members, cpToken) =
    static let empty = ProductionBuilder(ImmutableList.Empty, null)
    /// Creates a production builder whose members are the given objects.
    /// The objects must be either strings or characters (where they will be
    /// interpreted as literals), or designtime Farkles. Passing an object of
    /// different type will raise an exception.
    new ([<ParamArray>] members: obj []) =
        let members =
            members
            |> Seq.map (
                function
                | :? DesigntimeFarkle as df -> df
                | :? string as s -> Literal s :> DesigntimeFarkle
                | :? char as c -> c |> string |> Literal :> DesigntimeFarkle
                | x -> invalidArg "members" <| sprintf "Only designtime Farkles, strings and characters are \
allowed in a production builder's constructor. You provided a %O" (x.GetType()))
            |> ImmutableList.CreateRange
        ProductionBuilder(members, null)
    /// A production builder with no members.
    static member Empty = empty
    /// Creates a production builder from this one with the given untyped
    /// designtime Farkle added to the end as a not significant member.
    member _.Append(sym: DesigntimeFarkle) =
        nullCheck (nameof sym) sym
        ProductionBuilder(members.Add sym, cpToken)
    /// <summary>The <c>Append(DesigntimeFarkle)</c> method as an F# operator.</summary>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline (.>>) (x: ProductionBuilder, df: DesigntimeFarkle) =
        x.Append(df)
    /// Creates a production builder from this one with
    /// the given string added to the end as a literal.
    member x.Append(lit) = x.Append(Literal lit)
    /// <summary>The <c>Append(string)</c> method as an F# operator.</summary>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline (.>>) (x: ProductionBuilder, literal: string) =
        x.Append(literal)
    /// Creates a production builder from this one with the given typed
    /// designtime Farkle added to the end as a significant member.
    /// Up to sixteen significant members can be added to a production builder.
    member _.Extend<[<Nullable(0uy)>] 'T1>(df: DesigntimeFarkle<'T1>) =
        nullCheck (nameof df) df
        ProductionBuilder<'T1>(members.Add df, members.Count, cpToken)
    /// <summary>The <c>Extend</c> method as an F# operator.</summary>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline (.>>.) (x: ProductionBuilder, df) =
        x.Extend(df)
    /// <summary>Like <c>Finish</c>, but the given function accepts
    /// a read-only span of all the production's members as objects.</summary>
    /// <remarks>
    ///     <para>This method is intended to be used when finishing
    ///     a production with many significant members.</para>
    /// </remarks>
    member _.FinishRaw<[<Nullable(0uy)>] 'TOutput>(fuser: F<'TOutput>) =
        nullCheck (nameof fuser) fuser
        Production<'TOutput>(members, FuserData.CreateRaw fuser, cpToken)
    /// Finishes the production's construction and returns it.
    /// This method accepts an F# function that returns the production's output.
    member x.FinishFSharp<[<Nullable(0uy)>] 'TOutput>(f: _ -> 'TOutput) = x.FinishRaw(fun _ -> f())
    /// <summary>The <c>FinishFSharp</c> method as an F# operator.</summary>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member inline (=>) (x: ProductionBuilder, f) =
        x.FinishFSharp(f)
    /// Finishes the production's construction and returns it.
    /// This method accepts a delegate that returns the production's output.
    member _.Finish<[<Nullable(0uy)>] 'TOutput>(f: Func<'TOutput>) =
        nullCheck (nameof f) f
        let fuserData = FuserData.Create(f, F(fun _ -> f.Invoke()), [])
        Production<'TOutput>(members, fuserData, cpToken)
    /// <summary>Creates a <see cref="Production{T}"/> that
    /// always returns a constant value.</summary>
    member _.FinishConstant<[<Nullable(0uy)>] 'TOutput>(v: 'TOutput) =
        Production<'TOutput>(members, FuserData.CreateConstant v, cpToken)
    /// <summary>Returns a production builder with the given contextual precedence token.</summary>
    /// <param name="cpToken">An object that identifies the production
    /// when defining operator precedence and associativity.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="cpToken"/>
    /// is <see langword="null"/>.</exception>
    member _.WithPrecedence(cpToken) =
        nullCheck "cpToken" cpToken
        ProductionBuilder(members, cpToken)
    /// <summary>Returns a production builder with a unique contextual precedence token
    /// assigned to it, which is also returned by reference.</summary>
    /// <param name="cpTokenRef">The reference that will be assigned a newly created object
    /// which will serve as the production's contextual precedence token.</param>
    /// <remarks>This method allows a simpler experience for C# users.</remarks>
    member x.WithPrecedence(cpTokenRef: outref<_>) =
        let tok = box x
        cpTokenRef <- tok
        x.WithPrecedence(tok)
    interface IProduction with
        member _.Members = members.ToImmutableArray()
        member _.Fuser = FuserData.Null
        member _.ContextualPrecedenceToken = cpToken
