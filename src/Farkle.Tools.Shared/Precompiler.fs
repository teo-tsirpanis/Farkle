// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Precompiler

open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open Farkle.Monads.Either
open Mono.Cecil
open Serilog
open Sigourney
open System
open System.IO
open System.Reflection

type private LoggerWrapper(log: ILogger) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.Verbose(template) = log.Verbose(template)
    member _.Information(template, [<ParamArray>] args: _ []) = log.Information(template, args)
    member _.Error(template, [<ParamArray>] args: _ []) = log.Error(template, args)

let private getPrecompilableGrammarsImpl asm (log: LoggerWrapper) =
    asm
    |> Precompiler.Discoverer.discover
    |> List.map (fun pcdf ->
        let name = pcdf.Name
        log.Information("Precompiling {Grammar}...", name)
        let grammar =
            pcdf.Grammar
            |> DesigntimeFarkleBuild.buildGrammarOnly
        match grammar with
        | Ok grammar ->
            log.Information("Precompiling {Grammar} succeeded.")
            use stream = new MemoryStream()
            EGT.toStreamNeo stream grammar
            Some(name, stream.ToArray())
        | Error err ->
            log.Error("Error while precompiling {Grammar}: {ErrorMessage}", name, string err)
            None)
    |> List.allSome
    |> ofOption

type private DynamicDiscoverer(log: LoggerWrapper) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.GetPrecompilableGrammars(path) =
        log.Verbose("Hello from the other side")
        let asm = Assembly.LoadFile path
        getPrecompilableGrammarsImpl asm log

#if !NET472
open System.Runtime.Loader

type FarkleAwareAssemblyLoadContext() =
    inherit AssemblyLoadContext("Farkle.Tools precompiler", true)
    override x.Load(name) =
        // We want Farkle loaded only once.
        let farkleAssembly = typeof<DesigntimeFarkle>.Assembly
        if name.Name = farkleAssembly.GetName().Name then
            farkleAssembly
        else
            null

let ensureUnloaded (log: ILogger) (alc: byref<AssemblyLoadContext>) =
    let wr = WeakReference alc
    alc.Unload()
    alc <- null
    if wr.IsAlive then
        log.Debug("Running a full GC to free the assembly context...")
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        if wr.IsAlive then
            log.Warning("The assembly context was not collected! Writing to the assembly might fail.")
#endif

let getPrecompilableGrammars (log: ILogger) path =
    let logw = LoggerWrapper(log)
    #if NET472
    log.Debug("Creating AppDomain...")
    let ad = AppDomain.CreateDomain(sprintf "Farkle.Tools precompiler for %s" path)
    try
        let theHeroicPrecompiler =
            ad.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName,
                typeof<DynamicDiscoverer>.FullName,
                [|logw|])
            |> unbox<DynamicDiscoverer>
        theHeroicPrecompiler.GetPrecompilableGrammars path
    finally
        AppDomain.Unload(ad)
    #else
    let mutable alc = FarkleAwareAssemblyLoadContext() :> AssemblyLoadContext
    try
        let asm = alc.LoadFromAssemblyPath path
        getPrecompilableGrammarsImpl asm logw
    finally
        ensureUnloaded log &alc
    #endif

let weaveAssembly pcdfs (asm: AssemblyDefinition) =
    List.iter (fun (name, data: _ []) ->
        let name = Precompiler.Loader.getPrecompiledGrammarResourceName name
        let res = EmbeddedResource(name, Mono.Cecil.ManifestResourceAttributes.Private, data)
        asm.MainModule.Resources.Add res) pcdfs
    not pcdfs.IsEmpty

let precompile log path output = either {
    let! pcdfs = getPrecompilableGrammars log path

    do!
        pcdfs
        |> Seq.map fst
        |> Seq.countBy id
        |> Seq.map (fun (name, count) ->
            if count <> 1 then
                Log.Error("Cannot have many precompilable designtime Farkles named {Name}", name)
            count <> 1)
        |> Seq.fold (||) false
        |> (function true -> Error() | false -> Ok())

    Weaver.Weave(path, output, Converter(weaveAssembly pcdfs), log, WeaverConfig(), "Farkle")
}
