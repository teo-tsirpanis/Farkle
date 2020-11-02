// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Tools.Precompiler

#if !NETFRAMEWORK
open Farkle.Builder
open Farkle.Common
open Farkle.Grammar
open Mono.Cecil
open Serilog
open Sigourney
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.Loader

let private dynamicDiscoverAndPrecompile asm (log: ILogger) =
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

type private PrecompilerContext(path, references: AssemblyReference seq, log: ILogger) as this =
    inherit AssemblyLoadContext(sprintf "Farkle.Tools precompiler for %s" path, true)
    let dict =
        references
        |> Seq.choose (fun asm ->
            if asm.IsReferenceAssembly then
                None
            else
                log.Verbose("Using reference from {AssemblyPath}", asm.FileName)
                Some (asm.AssemblyName.FullName, path))
        |> readOnlyDict
    let asm =
        // We first read the assembly into a byte array to avoid the runtime locking the file.
        let bytes = File.ReadAllBytes path
        let m = new MemoryStream(bytes, false)
        this.LoadFromStream m
    member _.TheAssembly = asm
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
let private getPrecompilableGrammars log references path =
    let alc = PrecompilerContext(path, references, log)
    try
        let asm = alc.TheAssembly
        if asm.GetName().Name = typeof<DesigntimeFarkle>.Assembly.GetName().Name then
            log.Error("Cannot precompile an assembly named 'Farkle'")
            []
        else
            dynamicDiscoverAndPrecompile asm log
    finally
        alc.Unload()

let weaveAssembly pcdfs (asm: AssemblyDefinition) =
    List.iter (fun (name, data: _ []) ->
        let name = Precompiler.Loader.getPrecompiledGrammarResourceName name
        let res = EmbeddedResource(name, ManifestResourceAttributes.Public, data)
        asm.MainModule.Resources.Add res) pcdfs
    not pcdfs.IsEmpty

let discoverAndPrecompile log references path =
    let pcdfs = getPrecompilableGrammars log references path

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
#else
let discoverAndPrecompile _ _ _ = Ok []

let weaveAssembly _ _ = false
#endif
