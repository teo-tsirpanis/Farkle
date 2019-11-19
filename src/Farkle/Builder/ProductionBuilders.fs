// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
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
/// <see cref="AbstractProductionBuilder{TBuilder}.Append"/> can return the correct production builder type</typeparam>
type ProductionBuilder(members) =
    /// A production builder with no members.
    static member Empty = ProductionBuilder(ImmutableList.Empty)
    member __.Append(sym) = ProductionBuilder(Symbol.append members sym)
    member x.Append(lit) = x.Append(Literal lit)
    member __.Extend(df: DesigntimeFarkle<'T1>) = ProductionBuilder<'T1>(Symbol.append members df, members.Count)
    member x.FSharpFinish(fFuseThunk) = x.FinishRaw(fun _ -> fFuseThunk())
    member x.Finish(f: Func<_>) = x.FSharpFinish(FuncConvert.FromFunc(f))
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
    /// <summary>Creates a <see cref="Production{T}"/> that always returns a constant value.</summary>
    member x.FinishConstant(v) = x.FinishRaw(fun _ -> v)

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
/// F# operators to easily work with productions and their builders.
module DesigntimeFarkleOperators =

    /// Creates a terminal with the given name, specified by the given `Regex`.
    /// Its content will be post-processed by the given `T` delegate.
    let inline terminal name fTransform regex = Terminal.Create(name, fTransform, regex)

    /// Creates an untyped `DesigntimeFarkle` that recognizes a literal string
    let literal str = Literal str :> DesigntimeFarkle

    /// Creates a `Nonterminal` whose productions must be
    /// set with `SetProductions`, or it will raise an
    /// error. Useful for recursive productions.
    let nonterminal name = {
        _Name = name
        Productions = SetOnce<_>.Create()
    }

    /// Creates a `DesigntimeFarkle<'T>` that represents
    /// a nonterminal with the given name and productions.
    /// If an empty list of productions is given, an exception will be raised.
    let (||=) name members =
        match members with
        // Errors like that are caused by the user's API misuse.
        // That's why we must raise an exception.
        | [] -> failwithf "Cannot specify an empty list for <%s>'s productions." name
        | x :: xs ->
            let nont = nonterminal name
            nont.SetProductions(x, Array.ofList xs)
            nont :> DesigntimeFarkle<_>

    /// The `Append` method of production builders as an operator.
    // https://github.com/ionide/ionide-vscode-fsharp/issues/1203
    let inline op_DotGreaterGreater pb df = (^TBuilder : (member Append: ^TDesigntimeFarkle -> ^TBuilder) (pb, df))

    /// The `Extend` method of production builders as an operator.
    let inline op_DotGreaterGreaterDot pb df = (^TBuilder : (member Extend: DesigntimeFarkle<'T> -> ^TBuilderResult) (pb, df))

    /// The `Finish` method of production builders as an operator.
    let inline (=>) pb f = (^TBuilder : (member FSharpFinish: ^TFunction -> Production<'T>) (pb, f))

    /// `ProductionBuilder.FinishConstant` as an operator.
    let inline (=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)

    /// A production builder with no members.
    let empty = ProductionBuilder.Empty

    /// Creates a production builder with one non-significant `DesigntimeFarkle`.
    /// This function is useful to start building a `Production`.
    let inline (!%) (df: DesigntimeFarkle) = empty.Append(df)

    /// Creates a production builder with one non-significant string literal.
    let inline (!&) str = empty.Append(str: string)

    /// Creates a production builder with one significant `DesigntimeFarkle<'T>`.
    /// This function is useful to start building a `Production`.
    let inline (!@) (df: DesigntimeFarkle<'T>) = empty.Extend(df)
