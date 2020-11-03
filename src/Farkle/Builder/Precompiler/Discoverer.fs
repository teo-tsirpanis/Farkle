// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.Precompiler.Discoverer

open Farkle.Builder
open System
open System.Collections.Generic
open System.Reflection

[<Literal>]
let private nestedTypesBindingFlags =
    BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

[<Literal>]
let private fieldsBindingFlags =
    BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

/// Gets all `PrecompilableDesigntimeFarkle`s of the given assembly.
/// To be discovered, they must reside in a static read-only field
/// (in C#). or in an immutable let-bound value (in F#) that has been
/// applied the function `RuntimeFarkle.markForPrecompile`. The declared
/// type of that field must inherit `PrecompilableDesigntimeFarkle`.
let discover (asm: Assembly) =
    let probeType (typ: Type) =
        // F# values are properties, but are backed by a static read-only
        // field in a cryptic "<StartupCode$_>" namespace.
        typ.GetFields(fieldsBindingFlags)
        |> Seq.filter (fun fld ->
            // In the past, the PrecompilableDesigntimeFarkle type was
            // internal, meaning that we had to examine all static readonly
            // designtime Farkle fields for precompilability. However this
            // is no longer the case; the user can more explicitly mark a
            // designtime Farkle as precompilable, and the discoverer can be
            // more precise (code from another assembly is essentially executed
            // so we must be precise).
            typeof<PrecompilableDesigntimeFarkle>.IsAssignableFrom fld.FieldType
            && fld.IsInitOnly)
        |> Seq.map (fun fld -> fld.GetValue(null) :?> PrecompilableDesigntimeFarkle)
        |> Seq.filter (fun pcdf -> pcdf.Assembly = asm)

    let types = ResizeArray()
    let typesToProcess = Queue(asm.GetTypes())
    while typesToProcess.Count <> 0 do
        let typ = typesToProcess.Dequeue()
        types.Add typ
        for nestedType in typ.GetNestedTypes(nestedTypesBindingFlags) do
            typesToProcess.Enqueue nestedType
    types
    |> Seq.collect probeType
    // Precompiled designtime Farkles are F# object types, which
    // means they follow reference equality semantics.
    |> Seq.distinct
    |> List.ofSeq
