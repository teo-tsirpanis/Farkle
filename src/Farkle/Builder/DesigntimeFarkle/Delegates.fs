// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open System
open System.Runtime.CompilerServices

/// <summary>A delegate that transforms the content
/// of a terminal to an arbitrary object.</summary>
/// <param name="context">An <see cref="ITransformerContext"/>
/// that provides additional info about the terminal.</param>
/// <param name="data">A read-only span of the terminal's characters.</param>
/// <remarks>
/// <para>In F# this type is shortened to
/// <c>T</c> to avoid clutter in user code.</para>
/// <para>A .NET delegate was used because read-only
/// spans are incompatible with F# functions.</para>
/// </remarks>
[<CompiledName("Transformer`1")>]
type T<[<CovariantOut; Nullable(2uy)>] 'T> = delegate of context: ITransformerContext * data: ReadOnlySpan<char> -> 'T

/// <summary>A delegate that fuses the many members
/// of a production into one arbitrary object.</summary>
/// <param name="members">A read-only span of the production's members.</param>
/// <remarks>
/// <para>In F# this type is shortened to
/// <c>F</c> to avoid clutter in user code.</para>
/// <para>A .NET delegate was used because read-only
/// spans are incompatible with F# functions.</para>
/// </remarks>
[<CompiledName("Fuser`1")>]
type F<[<CovariantOut; Nullable(2uy)>] 'T> = delegate of members: ReadOnlySpan<obj> -> 'T

type internal TransformerData private(boxedDelegate) =
    static let tdNull =
        let tNull = T(fun _ _ -> null: obj)
        TransformerData(tNull)
    member _.BoxedDelegate = boxedDelegate
    static member Null = tdNull
    static member Create (t: T<'T>) =
        let tBoxed =
            // https://stackoverflow.com/questions/12454794
            if typeof<'T>.IsValueType then
                T(fun context data -> t.Invoke(context, data) |> box)
            else
                unbox t
        TransformerData(tBoxed)

type internal FuserData private(boxedDelegate) =
    static let fdNull =
        let fNull = F(fun _ -> null: obj)
        FuserData(fNull)
    static let boxF (f: F<'T>) =
        if typeof<'T>.IsValueType then
            F(fun data -> f.Invoke data |> box)
        else
            unbox f
    member _.BoxedDelegate = boxedDelegate
    static member Null = fdNull
    static member Create (f: F<'T>) =
        FuserData(boxF f)
    static member CreateRaw (f: F<'T>) =
        FuserData(boxF f)
    static member CreateAsIs idx =
        FuserData(F(fun x -> x[idx]))
    static member CreateConstant (x: 'T) =
        let xBoxed = box x
        let fBoxed = F(fun _ -> xBoxed)
        FuserData(fBoxed)
