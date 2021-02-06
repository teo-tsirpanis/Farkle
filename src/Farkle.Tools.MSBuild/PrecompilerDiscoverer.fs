// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Tools.PrecompilerDiscoverer

open Farkle.Builder
open Serilog
open System
open System.Reflection

[<Literal>]
let private allBindingFlags =
    BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

let private filterTargetInvocationException (e: exn) =
    match e with
    | :? TargetInvocationException as tie when not (isNull tie.InnerException) ->
        tie.InnerException
    | e -> e

/// Gets all `PrecompilableDesigntimeFarkle`s of the given assembly.
/// To be discovered, they must reside in a static read-only field
/// (in C#). or in an immutable let-bound value (in F#) that has been
/// applied the function `RuntimeFarkle.markForPrecompile`. The declared
/// type of that field must inherit `PrecompilableDesigntimeFarkle`.
let discover (log: ILogger) (asm: Assembly) =
    let probeType (typ: Type) =
        // F# values are properties, but are backed by a static read-only
        // field in a cryptic "<StartupCode$_>" namespace.
        typ.GetFields(allBindingFlags)
        |> Seq.filter (fun fld ->
            typeof<PrecompilableDesigntimeFarkle>.IsAssignableFrom fld.FieldType
            && (
                let mutable isEligible = true
                if not fld.IsStatic then
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it is not static.", fld.DeclaringType, fld.Name)
                    isEligible <- false
                if not fld.IsInitOnly then
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it is not readonly.", fld.DeclaringType, fld.Name)
                    isEligible <- false
                isEligible
            )
        )
        |> Seq.map (fun fld ->
            try
                fld.GetValue(null) :?> PrecompilableDesigntimeFarkle |> Ok
            with
            e -> Error(fld.DeclaringType.FullName, fld.Name, filterTargetInvocationException e))
        |> Seq.filter (function Ok pcdf -> pcdf.Assembly = asm | Error _ -> true)

    let types = asm.GetTypes()
    types
    |> Seq.filter (fun typ -> not typ.IsGenericType)
    |> Seq.collect probeType
    // Precompilable designtime Farkles are F# object types, which
    // means they follow reference equality semantics. If discovery
    // fails, its exception object will surely be unique so there
    // isn't a chance that an error misses being reported.
    |> Seq.distinct
    |> List.ofSeq
