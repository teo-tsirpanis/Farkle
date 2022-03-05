// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Farkle's in-process precompiler.
/// Only supports running on .NET Core and .NET. That does not mean
/// it only supports precompiling assemblies targeting these frameworks.
module Farkle.Tools.Precompiler.PrecompilerInProcess

open Farkle.Builder
open Farkle.Grammars
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
    | PrecompilingFailed of grammarName: string * GrammarDefinition * BuildError list
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
        log.Information("Precompiling {GrammarName:l}...", name)
        let grammarDef = pcdf.CreateGrammarDefinition()
        let grammar = DesigntimeFarkleBuild.buildGrammarOnlyEx ct BuildOptions.Default grammarDef
        match grammar with
        | Ok grammar ->
            // FsLexYacc does it, so why not us?
            log.Information("{GrammarName:l} was successfully precompiled: {Terminals} terminals, {Nonterminals} \
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
        | Error xs -> PrecompilingFailed(name, grammarDef, xs)
    | Error x -> DiscoveringFailed x

type private PrecompilerContext(path: string, references: AssemblyReference seq, log: ILogger) =
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
    member this.LoadFromAssemblyPathInMemory path =
        let symbolsPath = Path.ChangeExtension(path, ".pdb")
        use assemblyFile = File.OpenRead path
        use symbolsFile =
            try
                log.Debug("Reading symbols from {SymbolsPath}", symbolsPath)
                File.OpenRead symbolsPath :> Stream
            with
            | :? FileNotFoundException ->
                log.Debug("Symbols not found")
                Stream.Null
            | e ->
                Log.Debug(e, "Loading symbols failed")
                Stream.Null
        // The stream overload reads the assemblies in memory before loading them.
        // We do that to avoid the runtime locking the file.
        this.LoadFromStream(assemblyFile, symbolsFile)
    override this.Load(name) =
        log.Verbose("Requesting assembly {AssemblyName:l}.", name)
        match name.Name with
        | "Farkle" -> typeof<DesigntimeFarkle>.Assembly
        | "FSharp.Core" -> typeof<FuncConvert>.Assembly
        | "mscorlib" | "System.Private.CoreLib"
        | "System.Runtime" | "netstandard" -> null
        | _ ->
            match dict.TryGetValue name.FullName with
            | true, path ->
                log.Verbose("Loading assembly from {AssemblyPath:l}.", path)
                this.LoadFromAssemblyPath path
            | false, _ -> null

let rec private ensureWeakReferenceIsCollected (log: ILogger) (wr: WeakReference) numberOfTries =
    let rec impl remainingTries =
        match remainingTries, wr.IsAlive with
        | _, false ->
            log.Debug<_>("Assembly unloaded after {NumberOfCollections} garbage collections.", numberOfTries - remainingTries)
        | 0u, true ->
            log.Warning("The precompiler could not unload the assembly. This indicates a subtle bug in your code around \
the assembly's precompilable designtime Farkles and might lead to files and resources being held for longer than needed. \
You can learn how to diagnose it in https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability")
        | _, true ->
            log.Verbose("Running full GC")
            GC.Collect()
            GC.WaitForPendingFinalizers()
            impl (remainingTries - 1u)
    impl numberOfTries

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private precompileAssemblyFromPathIsolated ct log references path =
    let alc = PrecompilerContext(path, references, log)
    let wr = WeakReference(alc)
    try
        let asm = alc.LoadFromAssemblyPathInMemory path
        if asm.GetName().Name = typeof<DesigntimeFarkle>.Assembly.GetName().Name then
            log.Error("Cannot precompile an assembly named 'Farkle'.")
            []
        else
            discoverPrecompilableDesigntimeFarkles log asm
            |> List.map (precompileDiscovererResult ct log)
        , wr
    finally
        alc.Unload()

let private checkForDuplicates (log: ILogger) (pcdfs: _ list) =
    let foundDuplicates =
        pcdfs
        |> Seq.choose (
            function
            | Successful grammar  -> Some grammar.Properties.Name
            | PrecompilingFailed(name, _, _) -> Some name
            | _ -> None)
        |> Seq.countBy id
        |> Seq.map (fun (name, count) ->
            if count <> 1 then
                log.Error("Cannot have many precompilable designtime Farkles named {Name:l}.", name)
            count <> 1)
        |> Seq.fold (||) false
    
    if foundDuplicates then
        log.Information("You can rename a designtime Farkle with the DesigntimeFarkle.rename function \
or the Rename extension method.")

let private handleGrammarErrors (log: ILogger) fCreateConflictReport (errorMode: ErrorMode) name errors =
    log.Error<string>("Precompiling {GrammarName:l} failed.", name)
    // At most one conflict report can appear among the build errors.
    let conflictReport =
        errors
        |> List.tryPick (function BuildError.LALRConflictReport report -> Some report | _ -> None)

    let doEmitReport = errorMode <> ErrorMode.ErrorsOnly
    let doEmitConflictErrors = errorMode <> ErrorMode.ReportOnly

    errors
    |> Seq.filter (function
    | BuildError.LALRConflictReport _ -> false
    | BuildError.LALRConflict _ -> doEmitConflictErrors
    | _ -> true)
    |> Seq.iter (fun error -> log.Error("{BuildError:l}", error))

    match conflictReport with
    | Some report when doEmitReport ->
        if not doEmitConflictErrors then
            let conflictCount =
                errors
                |> Seq.filter (function BuildError.LALRConflict _ -> true | _ -> false)
                |> Seq.length
            match conflictCount with
            | 1 ->
                log.Error("There was one LALR conflict in the grammar.")
            | _ ->
                log.Error("There were {NumConflicts} LALR conflicts in the grammar.", conflictCount)
        fCreateConflictReport report
    | _ -> ()

let precompileAssemblyFromPath ct log fCreateConflictReport errorMode references path =
    let results, alcWeakReference = precompileAssemblyFromPathIsolated ct log references path
    ensureWeakReferenceIsCollected log alcWeakReference 5u

    checkForDuplicates log results
    results
    |> List.choose (fun x ->
        match x with
        | Successful grammar ->
            Some grammar
        | PrecompilingFailed(name, grammarDef, errors) ->
            handleGrammarErrors log (fCreateConflictReport grammarDef) errorMode name errors
            None
        | DiscoveringFailed(typeName, fieldName, e) ->
            log.Error("Exception thrown while getting the value of field {TypeName:l}.{FieldName:l}:", typeName, fieldName)
            log.Error("{Exception:l}", e)
            None)

let weaveGrammars (asm: AssemblyDefinition) (precompiledGrammars: _ list) =
    use stream = new MemoryStream()
    for grammar in precompiledGrammars do
        EGT.toStreamNeo stream grammar

        // We will try to read the EGTneo file we just
        // generated as a form of self-verification.
        stream.Position <- 0L
        EGT.ofStream stream |> ignore

        let name = BuilderCommon.getPrecompiledGrammarResourceName grammar
        let res = EmbeddedResource(name, ManifestResourceAttributes.Public, stream.ToArray())
        asm.MainModule.Resources.Add res
        stream.SetLength 0L
    not precompiledGrammars.IsEmpty
