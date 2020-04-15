// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Precompiler

open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open Mono.Cecil
open Serilog
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Reflection
open System.Runtime.CompilerServices

type private LoggerWrapper(log: ILogger) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.Verbose(template) = log.Verbose(template)
    member _.Information(template, [<ParamArray>] args: _ []) = log.Information(template, args)
    member _.Error(template, [<ParamArray>] args: _ []) = log.Error(template, args)

let private dynamicDiscoverAndPrecompile asm (log: LoggerWrapper) =
    asm
    |> Precompiler.Discoverer.discover
    |> List.map (fun pcdf ->
        let name = pcdf.Name
        log.Information("Precompiling {Grammar}...", name)
        let grammar = DesigntimeFarkleBuild.buildGrammarOnly pcdf.Grammar
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
            use stream = new MemoryStream()
            EGT.toStreamNeo stream grammar
            Ok(stream.ToArray())
        | Error msg -> Error(string msg)
        |> (fun x -> name, x))

type private DynamicDiscoverer(log: LoggerWrapper) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.DiscoverAndPrecompile(path) =
        log.Verbose("Hello from the other side")
        let asm = Assembly.LoadFile path
        dynamicDiscoverAndPrecompile asm log

let getAssemblyName (path: string) = AssemblyDefinition.ReadAssembly(path).Name.Name

#if NET472
let makeResolver references =
    let dict =
        references
        |> Seq.map (fun path -> KeyValuePair(getAssemblyName path, path))
        |> ImmutableDictionary.CreateRange
    ResolveEventHandler(fun _ e ->
        match e.Name with
        | "Farkle" -> null
        | name ->
            match dict.TryGetValue name with
            | true, path -> Assembly.LoadFile path
            | false, _ -> null)
#else
open System.Runtime.Loader

type private FarkleAwareAssemblyLoadContext(path, references, log: ILogger) =
    inherit AssemblyLoadContext(sprintf "Farkle.Tools precompiler for %s" path, true)
    let dict =
        references
        |> Seq.map (fun path -> KeyValuePair(getAssemblyName path, path))
        |> ImmutableDictionary.CreateRange
    override this.Load(name) =
        log.Verbose("Requesting assembly {AssemblyName}", name)
        match name.Name with
        | "Farkle" -> typeof<DesigntimeFarkle>.Assembly
        | "FSharp.Core" -> typeof<FuncConvert>.Assembly
        | "mscorlib" | "System.Private.CoreLib" | "System.Runtime" -> null
        | name ->
            match dict.TryGetValue name with
            | true, path -> this.LoadFromAssemblyPath path
            | false, _ -> null

let private ensureUnloaded (log: ILogger) (wr: WeakReference) =
    if wr.IsAlive then
        log.Debug("Running a full GC to free the assembly context...")
        let mutable i = 10
        while wr.IsAlive && (i > 0) do
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            i <- i - 1
        if wr.IsAlive then
            // Not getting unloaded does not affect weaving an assembly.
            // This is great news; a warning is not necessary.
            log.Debug("The assembly context was not collected after {GCTries} attempts! \
Writing to the assembly might fail.", 10 - i)
        else
            log.Debug("Assembly context unloaded after {GCTries} tries.", 10 - i)
#endif

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private getPrecompilableGrammars (log: ILogger) references path =
    let logw = LoggerWrapper(log)
    #if NET472
    log.Debug("Creating AppDomain...")
    let ad = AppDomain.CreateDomain(sprintf "Farkle.Tools precompiler for %s" path)
    try
        ad.add_AssemblyResolve(makeResolver references)
        let theHeroicPrecompiler =
            ad.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName,
                typeof<DynamicDiscoverer>.FullName,
                [|logw|])
            |> unbox<DynamicDiscoverer>
        theHeroicPrecompiler.DiscoverAndPrecompile path, WeakReference(obj())
    finally
        // Fody does that, no idea why. Maybe it's needed
        // because the lifetime service is set to null.
        Runtime.Remoting.RemotingServices.Disconnect logw |> ignore
        AppDomain.Unload(ad)
    #else
    let alc = FarkleAwareAssemblyLoadContext(path, references, log) :> AssemblyLoadContext
    try
        let asm = alc.LoadFromAssemblyPath path
        dynamicDiscoverAndPrecompile asm logw, WeakReference(alc)
    finally
        alc.Unload()
        log.Verbose("The context is at generation {Generation}", GC.GetGeneration(alc))
    #endif

let weaveAssembly pcdfs (asm: AssemblyDefinition) =
    List.iter (fun (name, data: _ []) ->
        let name = Precompiler.Loader.getPrecompiledGrammarResourceName name
        let res = EmbeddedResource(name, Mono.Cecil.ManifestResourceAttributes.Public, data)
        asm.MainModule.Resources.Add res) pcdfs
    not pcdfs.IsEmpty

let discoverAndPrecompile log references path =
    let pcdfs, wr = getPrecompilableGrammars log references path
    #if NET472
    ignore wr
    #else
    ensureUnloaded log wr
    #endif

    pcdfs
    |> Seq.map fst
    |> Seq.countBy id
    |> Seq.map (fun (name, count) ->
        if count <> 1 then
            log.Error("Cannot have many precompilable designtime Farkles named {Name}", name)
        count <> 1)
    |> Seq.fold (||) false
    |> (function
        | true ->
            log.Information("You can rename a designtime Farkle with the DesigntimeFarkle.rename function, \
or the Rename extension method.")
            Error()
        | false -> Ok pcdfs)

