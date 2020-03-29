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
/// type of that field must inherit `DesigntimeFarkle`.
let discover (asm: Assembly) =
    let probeType (typ: Type) =
        let fromProperties =
            typ.GetProperties(fieldsBindingFlags)
            |> Seq.filter (fun prop ->
                // We can't know at runtime whether the designtime Farkle is
                // precompilable; we have to first load all designtime Farkles.
                typeof<DesigntimeFarkle>.IsAssignableFrom prop.PropertyType
                && (not <| isNull prop.GetMethod)
                && isNull prop.SetMethod)
            |> Seq.map (fun prop -> prop.GetValue(null))
        let fromFields =
            typ.GetFields(fieldsBindingFlags)
            |> Seq.filter (fun fld ->
                typeof<DesigntimeFarkle>.IsAssignableFrom fld.FieldType
                && fld.IsInitOnly)
            |> Seq.map (fun fld -> fld.GetValue(null))
        Seq.append fromProperties fromFields
        |> Seq.choose tryUnbox<PrecompilableDesigntimeFarkle>
        |> Seq.filter (fun pcdf -> pcdf.DeclaringAssembly = asm)
    let types = ResizeArray()
    let typesToProcess = Queue(asm.GetTypes())
    while typesToProcess.Count <> 0 do
        let typ = typesToProcess.Dequeue()
        types.Add typ
        nestedTypesBindingFlags
        |> typ.GetNestedTypes
        |> Array.iter typesToProcess.Enqueue
    types
    |> Seq.collect probeType
    |> List.ofSeq
