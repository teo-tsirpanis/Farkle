// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Common

open System
open System.Collections.Generic
open System.Reflection
open System.Threading

/// Can be set only once even when called concurrently.
/// This type is a mutable value type. It must be passed
/// by reference and not stored in a readonly field or
/// non-mutable let-bound value.
[<Struct>]
type internal Latch = private {
    mutable IsSet: int
}
with
    /// Creates a latch with the given initial state.
    static member Create isSet = {IsSet = if isSet then 1 else 0}
    /// Tries to set the latch. This function will return true
    /// only once and on one thread per instance.
    member x.TrySet() = Interlocked.Exchange(&x.IsSet, 1) = 0
    /// Sets the latch without caring whether it actually succeeded.
    member x.Set() = x.TrySet() |> ignore

/// Functions to work with the `FSharp.Core.Result` type.
module internal Result =

    let tee fOk fError =
        function
        | Ok x -> fOk x
        | Error x -> fError x

    /// Consolidates a sequence of `Result`s into a `Result` of a list.
    /// Errors are consilidated into a list as well.
    let collect xs = Seq.foldBack (fun x xs ->
        match x, xs with
        | Ok x, Ok xs -> Ok <| x :: xs
        | Error x, Ok _ -> Error [x]
        | Ok _, (Error _ as xs) -> xs
        | Error x, Error xs -> Error <| x :: xs) xs (Ok [])

    /// Returns a `Result` that is successful if both given results
    /// are successful, and is failed if at least one of them is failed.
    /// In the former case the returned result will carry its parameters'
    /// values, and in the latter it will carry their combined errors.
    let combine x1 x2 =
        match x1, x2 with
        | Ok x1, Ok x2 -> Ok(x1, x2)
        | Ok _, Error x
        | Error x, Ok _ -> Error x
        | Error x1, Error x2 -> Error(x1 @ x2)

    /// Returns the value of a `Result` or raises an exception.
    let returnOrFail result = tee id (failwithf "%O") result

module internal Reflection =
    let getAssemblyInformationalVersion (asm: Assembly) =
        let versionString =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        match versionString.IndexOf('+') with
        | -1 -> versionString
        | plusPosition -> versionString.Substring(0, plusPosition)

[<AutoOpen>]
module internal ErrorHandling =

    /// Raises an exception if `x` is null.
    let inline nullCheck<'T when 'T: not struct> argName (x: 'T) =
        if obj.ReferenceEquals(x, null) then
            nullArg argName

module internal Delegate =

    #if NET
    open System.Diagnostics.CodeAnalysis
    open System.Runtime.CompilerServices
    #endif

    /// Creates a delegate from an arbitrary
    /// object's `Invoke` method. Useful turning
    /// optimized closures to delegates without
    /// an extra level of indirection.
#if NET
    [<UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "The function's public methods are kept by DynamicallyAccessedMembers.")>]
    // The compiler randomly decides to inline it and causes trim warnings to other methods.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
#endif
    let ofInvokeMethod<'TDelegate,
                        #if NET
                        // We have to tell the IL Linker to spare the Invoke method.
                        [<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)>]
                        #endif
                        'TFunction
        when 'TDelegate :> Delegate and 'TFunction : not struct> (func: 'TFunction) =
        Delegate.CreateDelegate(typeof<'TDelegate>, func, "Invoke", false, true) :?> 'TDelegate

    /// Returns whether the delegate is closed over its first argument.
    let isClosed (del: Delegate) =
        del.Method.GetParameters().Length <> del.GetType().GetMethod("Invoke").GetParameters().Length

/// Object comparers that compare strings in a specific way if both
/// objects are strings. Otherwise they use the default comparer.
module internal FallbackStringComparers =
    let private create (comparer: StringComparer) =
        {new EqualityComparer<obj>() with
            member _.Equals(x1, x2) =
                match x1, x2 with
                // Without parentheses, lit2 is inferred to be the tuple of (x1, x2).
                // Code still compiles but fails at runtime because objects of different
                // types are compared.
                | (:? string as lit1), (:? string as lit2) ->
                    comparer.Equals(lit1, lit2)
                | _ -> EqualityComparer.Default.Equals(x1, x2)
            member _.GetHashCode x =
                match x with
                | null -> 0
                | :? string as lit -> 2 * comparer.GetHashCode(lit)
                | _ -> 2 * x.GetHashCode() + 1}

    let caseSensitive = create StringComparer.Ordinal

    let caseInsensitive = create StringComparer.OrdinalIgnoreCase

    let get isCaseSensitive = if isCaseSensitive then caseSensitive else caseInsensitive
