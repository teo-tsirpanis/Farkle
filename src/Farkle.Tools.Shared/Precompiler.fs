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
open System.Runtime.CompilerServices

type private LoggerWrapper(log: ILogger) =
#if NET472
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
#endif
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
            log.Information("Precompiling {Grammar} succeeded.", name)
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

type FarkleAwareAssemblyLoadContext(path) =
    inherit AssemblyLoadContext(sprintf "Farkle.Tools precompiler for %s" path, true)
    override x.Load(name) =
        // We want Farkle loaded only once.
        let farkleAssembly = typeof<DesigntimeFarkle>.Assembly
        if name.Name = farkleAssembly.GetName().Name then
            farkleAssembly
        else
            null

let private ensureUnloaded (log: ILogger) (wr: WeakReference) =
    if wr.IsAlive then
        log.Debug("Running a full GC to free the assembly context...")
        let mutable i = 10
        while wr.IsAlive && (i > 0) do
            GC.Collect()
            GC.WaitForPendingFinalizers()
            i <- i - 1
        if wr.IsAlive then
            // Not getting unloaded does not affect weaving an assembly.
            // This is great news.
            log.Debug("The assembly context was not collected after {GCTries} attempts! \
Writing to the assembly might fail.", 10 - i)
        else
            log.Debug("Assembly context unloaded after {GCTries} tries.", 10 - i)
#endif

[<MethodImpl(MethodImplOptions.NoInlining)>]
let getPrecompilableGrammars (log: ILogger) references path =
    let logw = LoggerWrapper(log)
    #if NET472
    log.Debug("Creating AppDomain...")
    let ad = AppDomain.CreateDomain(sprintf "Farkle.Tools precompiler for %s" path)
    try
        for r in references do
            r |> Path.GetFullPath |> ad.Load |> ignore
        let theHeroicPrecompiler =
            ad.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName,
                typeof<DynamicDiscoverer>.FullName,
                [|logw|])
            |> unbox<DynamicDiscoverer>
        theHeroicPrecompiler.GetPrecompilableGrammars path, WeakReference(obj())
    finally
        AppDomain.Unload(ad)
    #else
    let alc = FarkleAwareAssemblyLoadContext(path) :> AssemblyLoadContext
    try
        let asm = alc.LoadFromAssemblyPath path
        for r in references do
            r |> Path.GetFullPath |> alc.LoadFromAssemblyPath |> ignore
        getPrecompilableGrammarsImpl asm logw, WeakReference(alc)
    finally
        alc.Unload()
    #endif

let weaveAssembly pcdfs (asm: AssemblyDefinition) =
    List.iter (fun (name, data: _ []) ->
        let name = Precompiler.Loader.getPrecompiledGrammarResourceName name
        let res = EmbeddedResource(name, Mono.Cecil.ManifestResourceAttributes.Private, data)
        asm.MainModule.Resources.Add res) pcdfs
    not pcdfs.IsEmpty

let precompile log path output = either {
    let pcdfs, wr = getPrecompilableGrammars log ["Chiron.dll"; "FsLexYacc.Runtime.dll"] path
    let! pcdfs = pcdfs
    #if !NET472
    ensureUnloaded log wr
    #endif

    do!
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
            | false -> Ok())

    Weaver.Weave(path, output, Converter(weaveAssembly pcdfs), log, WeaverConfig(), "Farkle")
}
