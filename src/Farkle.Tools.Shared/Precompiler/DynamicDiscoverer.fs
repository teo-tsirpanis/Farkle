// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Precompiler

open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open Farkle.Monads.Either
open Serilog
open System
open System.IO
open System.Reflection

type private LoggerWrapper(log: ILogger) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.Verbose(template) = log.Verbose(template)
    member _.Information(template, [<ParamArray>] args: _ []) = log.Information(template, args)
    member _.Error(template, [<ParamArray>] args: _ []) = log.Error(template, args)

type private DynamicDiscoverer(log: LoggerWrapper) =
    inherit MarshalByRefObject()
    override _.InitializeLifetimeService() = null
    member _.GetPrecompilableGrammars(path) =
        log.Verbose("Hello from the other side")
        let asm = Assembly.LoadFile(path)
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
                log.Information("Precompiling {Grammar} suceeded.")
                use stream = new MemoryStream()
                EGT.toStreamNeo stream grammar
                Some(name, stream.ToArray())
            | Error err ->
                log.Error("Error while precompiling {Grammar}: {ErrorMessage}", name, string err)
                None)
        |> List.allSome
        |> ofOption

let getPrecompilableGrammars (log: ILogger) path =
    #if NET472
    log.Debug("Creating AppDomain...")
    let ad = AppDomain.CreateDomain(sprintf "Farkle precompiler for %s" path)
    try
        let logWrapper = LoggerWrapper(log)
        let theHeroicPrecompiler =
            ad.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName,
                typeof<DynamicDiscoverer>.FullName,
                [|logWrapper|])
            |> unbox<DynamicDiscoverer>
        theHeroicPrecompiler.GetPrecompilableGrammars path
    finally
        AppDomain.Unload(ad)
    #else
    log.Error("Not yet implemented for .NET Core...")
    Error()
    #endif

let precompile log path = either {
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

    // TODO
}
