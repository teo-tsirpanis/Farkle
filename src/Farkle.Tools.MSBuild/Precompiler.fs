// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Tools.Precompiler

open Farkle.Builder
open Farkle.Grammar

type PrecompilerResult =
    | Successful of Grammar
    | PrecompilingFailed of grammarName: string * BuildError list
    | DiscoveringFailed of typeName: string * fieldName: string * exn

#if !NETFRAMEWORK
open Farkle.Common
open Mono.Cecil
open Serilog
open Sigourney
open System.Diagnostics
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.Loader

let private dynamicDiscoverAndPrecompile asm (log: ILogger) =
    asm
    |> PrecompilerDiscoverer.discover
    |> List.map (function
    | Ok pcdf ->
        let name = pcdf.Name
        log.Information("Precompiling {GrammarName}...", name)
        let grammar = DesigntimeFarkleBuild.buildGrammarOnly pcdf.GrammarDefinition
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

            Debug.Assert(grammar.Properties.Name = name, "Unexpected error: grammar name is different from the designtime Farkle name.")
            Successful grammar
        | Error xs -> PrecompilingFailed(name, xs)
    | Error x -> DiscoveringFailed x)

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
                log.Verbose("{AssemblyName}: {AssemblyPath}", asm.AssemblyName.Name, asm.FileName)
                Some (asm.AssemblyName.FullName, asm.FileName))
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
    for grammar in pcdfs do
        use stream = new MemoryStream()
        EGT.toStreamNeo stream grammar

        // We will try to read the EGTneo file we just
        // generated as a form of self-verification.
        stream.Position <- 0L
        EGT.ofStream stream |> ignore

        let name = PrecompiledGrammar.GetResourceName grammar
        let res = EmbeddedResource(name, ManifestResourceAttributes.Public, stream.ToArray())
        asm.MainModule.Resources.Add res
    not (List.isEmpty pcdfs)

let discoverAndPrecompile log references path =
    let pcdfs = getPrecompilableGrammars log references path

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
#else
let discoverAndPrecompile (log: Serilog.ILogger) _ _ =
    log.Warning("Farkle can only precompile grammars on projects built with the .NET Core SDK (dotnet build etc). \
Your project will still work, but without the benefits the precompiler offers. \
See more in https://teo-tsirpanis.github.io/Farkle/the-precompiler.html#Building-from-an-IDE")
    Ok []

let weaveAssembly _ _ = false
#endif
