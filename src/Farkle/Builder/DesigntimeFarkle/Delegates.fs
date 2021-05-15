// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open System
open System.Runtime.CompilerServices

[<CompiledName("Transformer`1")>]
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
type T<[<CovariantOut; Nullable(2uy)>] 'T> = delegate of context: ITransformerContext * data: ReadOnlySpan<char> -> 'T

[<CompiledName("Fuser`1")>]
/// <summary>A delegate that fuses the many members
/// of a production into one arbitrary object.</summary>
/// <param name="members">A read-only span of the production's members.</param>
/// <remarks>
/// <para>In F# this type is shortened to
/// <c>F</c> to avoid clutter in user code.</para>
/// <para>A .NET delegate was used because read-only
/// spans are incompatible with F# functions.</para>
/// </remarks>
type F<[<CovariantOut; Nullable(2uy)>] 'T> = delegate of members: ReadOnlySpan<obj> -> 'T

type internal IRawDelegateProvider =
    abstract IsNull: bool
    abstract IsConstant: bool
    abstract RawDelegate: Delegate
    abstract ReturnType: Type

type internal TransformerData private(rawDelegate: Delegate, boxedDelegate, returnType) =
    static let tdNull =
        let tNull = T(fun _ _ -> null: obj)
        TransformerData(tNull, tNull, typeof<obj>)
    // This is used by the dynamic code generator.
    member _.RawDelegate = rawDelegate
    // And this is used by the static post-processor.
    member _.BoxedDelegate = boxedDelegate
    member _.ReturnType = returnType
    static member Null = tdNull
    static member Create (t: T<'T>) =
        let tBoxed =
            // https://stackoverflow.com/questions/12454794
            if typeof<'T>.IsValueType then
                T(fun context data -> t.Invoke(context, data) |> box)
            else
                unbox t
        TransformerData(t, tBoxed, typeof<'T>)
    interface IRawDelegateProvider with
        member x.IsNull = x = tdNull
        member _.IsConstant = false
        member _.RawDelegate = rawDelegate
        member _.ReturnType = returnType

type internal FuserData private(rawDelegate: Delegate, boxedDelegate, returnType, parameters: (int * Type) list, constant) =
    static let typedefofFuser = typedefof<F<_>>
    // Fusers returning null will be special-cased anyway.
    // There's no reason to model them as constants.
    static let fdNull =
        let fNull = F(fun _ -> null: obj)
        FuserData(fNull, fNull, typeof<obj>, [], ValueNone)
    static let fAsIs = Func<obj,obj>(fun x -> x)
    static let boxF (f: F<'T>) =
        if typeof<'T>.IsValueType then
            F(fun data -> f.Invoke data |> box)
        else
            unbox f
    member _.RawDelegate = rawDelegate
    member _.BoxedDelegate = boxedDelegate
    member _.ReturnType = returnType
    member _.Parameters = parameters
    member _.Constant = constant
    member _.IsAsIs = rawDelegate = upcast fAsIs
    member _.IsRaw = Type.op_Equality(rawDelegate.GetType(), typedefofFuser.MakeGenericType(returnType))
    static member Null = fdNull
    static member Create (fRaw, f: F<'T>, parameters) =
        FuserData(fRaw, boxF f, typeof<'T>, parameters, ValueNone)
    static member CreateRaw (f: F<'T>) =
        FuserData(f, boxF f, typeof<'T>, [], ValueNone)
    static member CreateAsIs idx =
        FuserData.Create(fAsIs, F(fun x -> x.[idx]), [idx, typeof<obj>])
    static member CreateConstant (x: 'T) =
        let xBoxed = box x
        let fBoxed = F(fun _ -> xBoxed)
        FuserData(fBoxed, fBoxed, typeof<'T>, [], ValueSome xBoxed)
    interface IRawDelegateProvider with
        member x.IsNull = x = fdNull
        member _.IsConstant = constant.IsSome
        member _.RawDelegate = rawDelegate
        member _.ReturnType = returnType
