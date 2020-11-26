// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open System

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
type T<[<CovariantOut>] 'T> = delegate of context: ITransformerContext * data: ReadOnlySpan<char> -> 'T

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
type F<[<CovariantOut>] 'T> = delegate of members: ReadOnlySpan<obj> -> 'T

type IRawDelegateProvider =
    abstract RawDelegate: Delegate
    abstract ReturnType: Type

type internal TransformerData private(rawDelegate: Delegate, boxedDelegate, returnType) =
    static let tNull = T(fun _ _ -> null: obj)
    static let tdNull = TransformerData(tNull, tNull, typeof<obj>)
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
        member _.RawDelegate = rawDelegate
        member _.ReturnType = returnType

type internal FuserData private(rawDelegate: Delegate, boxedDelegate, returnType, parameters: (int * Type) list, constant) =
    static let fNull = F(fun _ -> null: obj)
    // Fusers returning null will be special-cased anyway.
    // There's no reason to model them as constants.
    static let fdNull = FuserData(fNull, fNull, typeof<obj>, [], ValueNone)
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
    static member Null = fdNull
    static member Create (fRaw, f: F<'T>, parameters) =
        FuserData(fRaw, boxF f, typeof<'T>, parameters, ValueNone)
    static member CreateRaw (f: F<'T>) =
        FuserData(f, boxF f, typeof<'T>, [], ValueNone)
    static member CreateConstant (x: 'T) =
        let xBoxed = box x
        let fBoxed = F(fun _ -> xBoxed)
        FuserData(null, fBoxed, typeof<'T>, [], ValueSome xBoxed)

module internal T =
    /// Converts a `T` callback so that it returns an object.
    let box (f: T<'T>) =
        // https://stackoverflow.com/questions/12454794
        if typeof<'T>.IsValueType then
            T(fun context data -> f.Invoke(context, data) |> box)
        else
            unbox f

module internal F =
    /// Converts an `F` delegate so that it returns an object.
    let box (f: F<'T>) =
        if typeof<'T>.IsValueType then
            F(fun data -> f.Invoke(data) |> box)
        else
            unbox f
