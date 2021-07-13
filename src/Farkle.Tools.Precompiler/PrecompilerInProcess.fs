// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Farkle's in-process precompiler.
/// Only supports running on .NET Core and .NET 5+. That does not mean
/// it only supports precompiling assemblies targeting these frameworks.
module Farkle.Tools.PrecompilerInProcess

open Farkle.Builder
open Farkle.Grammar
open Serilog
open Sigourney
open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.Loader
// It has to be put in the end because
// ManifestResourceAttributes is hidden by System.Reflection.
open Mono.Cecil

type PrecompilerResult =
    | Successful of Grammar
    | PrecompilingFailed of grammarName: string * BuildError list
    | DiscoveringFailed of typeName: string * fieldName: string * exn

[<Literal>]
let private allBindingFlags =
    BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic
    ||| BindingFlags.DeclaredOnly

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
let private discoverPrecompilableDesigntimeFarkles (log: ILogger) (asm: Assembly) =
    let probeType (typ: Type) =
        // F# values are properties, but are backed by a static read-only
        // field in a cryptic "<StartupCode$_>" namespace.
        typ.GetFields(allBindingFlags)
        |> Seq.filter (fun fld ->
            typeof<PrecompilableDesigntimeFarkle>.IsAssignableFrom fld.FieldType
            && (
                let mutable isEligible = true
                if fld.DeclaringType.IsGenericType then
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it \
is declared in a generic type.", fld.DeclaringType, fld.Name)
                    isEligible <- false
                if not fld.IsStatic then
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it is not static.",
                        fld.DeclaringType, fld.Name)
                    isEligible <- false
                if not fld.IsInitOnly then
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it is not readonly.",
                        fld.DeclaringType, fld.Name)
                    isEligible <- false
                isEligible
            )
        )
        |> Seq.choose (fun fld ->
            try
                let pcdf = fld.GetValue(null) :?> PrecompilableDesigntimeFarkle
                if pcdf.Assembly = asm then
                    pcdf |> Ok |> Some
                else
                    log.Warning("Field {FieldType:l}.{FieldName:l} will not be precompiled because it was \
marked in a foreign assembly ({AssemblyName:l}).", fld.DeclaringType, fld.Name, pcdf.Assembly.GetName().Name)
                    None
            with
            e -> Error(fld.DeclaringType.FullName, fld.Name, filterTargetInvocationException e) |> Some)

    let types = asm.GetTypes()
    types
    |> Seq.collect probeType
    // Precompilable designtime Farkles are F# object types, which
    // means they follow reference equality semantics. If discovery
    // fails, its exception object will surely be unique so there
    // isn't a chance that an error misses being reported.
    |> Seq.distinct
    |> List.ofSeq

let private precompileDiscovererResult ct (log: ILogger) x =
    match x with
    | Ok (pcdf: PrecompilableDesigntimeFarkle) ->
        let name = pcdf.Name
        log.Information("Precompiling {GrammarName}...", name)
        let grammar =
            pcdf.CreateGrammarDefinition()
            |> DesigntimeFarkleBuild.buildGrammarOnlyEx ct BuildOptions.Default
        match grammar with
        | Ok grammar ->
            // FsLexYacc does it, so why not us?
            log.Information("{Grammar} was successfully precompiled: {Terminals} terminals, {Nonterminals} \
nonterminals, {Productions} productions, {LALRStates} LALR states, {DFAStates} DFA states",
                name,
                grammar.Symbols.Terminals.Length,
                grammar.Symbols.Nonterminals.Length,
                grammar.Productions.Length,
                grammar.LALRStates.Length,
                grammar.DFAStates.Length)

            Debug.Assert(grammar.Properties.Name = name, "Unexpected error: grammar name \
is different from the designtime Farkle name.")
            Successful grammar
        | Error xs -> PrecompilingFailed(name, xs)
    | Error x -> DiscoveringFailed x

type private PrecompilerContext(path: string, references: AssemblyReference seq, log: ILogger) as this =
    inherit AssemblyLoadContext(
        sprintf "Farkle.Tools precompiler for %s" (Path.GetFileNameWithoutExtension path),
        true)
    let dict =
        log.Verbose("References:")
        references
        |> Seq.choose (fun asm ->
            if asm.IsReferenceAssembly then
                None
            else
                log.Verbose("{AssemblyName:l}: {AssemblyPath}", asm.AssemblyName.Name, asm.FileName)
                Some (asm.AssemblyName.FullName, asm.FileName))
        |> readOnlyDict
    let theAssembly =
        // We first read the assembly into a byte array to avoid the runtime locking the file.
        let bytes = File.ReadAllBytes path
        let m = new MemoryStream(bytes, false)
        this.LoadFromStream m
    member _.TheAssembly = theAssembly
    override this.Load(name) =
        log.Verbose("Requesting assembly {AssemblyName}", name)
        match name.Name with
        | "Farkle" -> typeof<DesigntimeFarkle>.Assembly
        | "FSharp.Core" -> typeof<FuncConvert>.Assembly
        | "mscorlib" | "System.Private.CoreLib"
        | "System.Runtime" | "netstandard" -> null
        | _ ->
            match dict.TryGetValue name.FullName with
            | true, path ->
                log.Verbose("Loading assembly from {AssemblyPath}", path)
                this.LoadFromAssemblyPath path
            | false, _ -> null

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private precompileAssemblyFromPathIsolated ct log references path =
    let alc = PrecompilerContext(path, references, log)
    try
        let asm = alc.TheAssembly
        if asm.GetName().Name = typeof<DesigntimeFarkle>.Assembly.GetName().Name then
            log.Error("Cannot precompile an assembly named 'Farkle'")
            []
        else
            discoverPrecompilableDesigntimeFarkles log asm
            |> List.map (precompileDiscovererResult ct log)
    finally
        alc.Unload()

let private checkForDuplicates (log: ILogger) (pcdfs: _ list) =
    pcdfs
    |> Seq.choose (
        function
        | Successful grammar  -> Some grammar.Properties.Name
        | PrecompilingFailed(name, _) -> Some name
        | _ -> None)
    |> Seq.countBy id
    |> Seq.map (fun (name, count) ->
        if count <> 1 then
            log.Error("Cannot have many precompilable designtime Farkles named {Name}", name)
        count <> 1)
    |> Seq.fold (||) false
    |> function
        | true ->
            log.Information("You can rename a designtime Farkle with the DesigntimeFarkle.rename function \
or the Rename extension method.")
            Error()
        | false -> Ok pcdfs

let precompileAssemblyFromPath ct log references path =
    let pcdfs = precompileAssemblyFromPathIsolated ct log references path
    checkForDuplicates log pcdfs
    |> Result.map (List.choose (fun x ->
        match x with
        | Successful grammar ->
            Some grammar
        | PrecompilingFailed(name, [error]) ->
            log.Error("Error while precompiling {GrammarName}: {ErrorMessage}", name, error)
            None
        | PrecompilingFailed(name, errors) ->
            log.Error("Errors while precompiling {GrammarName}.", name)
            for error in errors do
                log.Error("{BuildError}", error)
            None
        | DiscoveringFailed(typeName, fieldName, e) ->
            log.Error("Exception thrown while getting the value of field {FieldName} in type {TypeName}.", fieldName, typeName)
            log.Error("{Exception}", e)
            None))

let weaveGrammars (asm: AssemblyDefinition) (precompiledGrammars: _ list) =
    use stream = new MemoryStream()
    for grammar in precompiledGrammars do
        EGT.toStreamNeo stream grammar

        // We will try to read the EGTneo file we just
        // generated as a form of self-verification.
        stream.Position <- 0L
        EGT.ofStream stream |> ignore

        let name = PrecompiledGrammar.GetResourceName grammar
        let res = EmbeddedResource(name, ManifestResourceAttributes.Public, stream.ToArray())
        asm.MainModule.Resources.Add res
        stream.SetLength 0L
    not precompiledGrammars.IsEmpty
