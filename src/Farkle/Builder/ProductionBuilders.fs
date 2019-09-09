// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

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
///     but the given <see cref="DesigntimeFarkle"/> at the end of it.</para>
///     <para><c>Extend</c> returns a production builder with one more significant member,
///     whose type is determined by the given <see cref="DesigntimeFarkle{T}"/> that will
///     be appended to it.</para>
///     <para><c>Finish</c> accepts a function that converts all the builder's significant members
///     into the eventual type of the returned <see cref="Production{T}"/>.</para>
/// </remarks>
/// <typeparam name="T">The type of the concrete production builder. Used so that
/// <see cref="AbstractProductionBuilder{TBuilder}.Append"/> can return the correct production builder type</typeparam>
type ProductionBuilder(members) =
    member __.Append(sym) = ProductionBuilder(Symbol.append members sym)
    member __.Extend(df: DesigntimeFarkle<'T1>) = ProductionBuilder<'T1>(Symbol.append members df, members.Count)
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

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
/// F# operators to easily work with productions and their builders.
module DesigntimeFarkleOperators =

    let private tNull: T<obj> = T(fun _ _ -> null)

    /// Creates a `Terminal`.
    let inline terminal name fTransform regex = Terminal.Create name fTransform regex

    /// Creates an untyped `DesigntimeFarkle` that recognizes a literal string
    let literal str = terminal str tNull (Regex.literal str) :> DesigntimeFarkle

    /// Creates an empty `Nonterminal`.
    /// It must be filled afterwards, or it will raise an error.
    let nonterminal name = Nonterminal.Create(name)

    /// Creates a `DesigntimeFarkle<'T>` with the given name and productions.
    let inline (||=) name parts = Nonterminal.Create(name, Array.ofSeq parts) :> DesigntimeFarkle<_>

    /// The `Append` method of production builders as an operator.
    // https://github.com/ionide/ionide-vscode-fsharp/issues/1203
    let inline op_DotGreaterGreater pb df = (^TBuilder : (member Append: DesigntimeFarkle -> ^TBuilder) (pb, df))

    /// The `Extend` method of production builders as an operator.
    let inline op_DotGreaterGreaterDot pb df = (^TBuilder : (member Extend: DesigntimeFarkle<'T> -> ^TBuilderResult) (pb, df))

    /// The `Finish` method of production builders as an operator.
    let inline (=>) pb f = (^TBuilder : (member Finish: ^TFunction -> Production<'T>) (pb, f))

    /// `ProductionBuilder.FinishConstant` as an operator.
    let inline (=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)
