// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Builder
open Farkle.Grammar
open System
open System.Reflection
open System.Runtime.CompilerServices

module DFB = DesigntimeFarkleBuild

type PrecompilerLoaderException(msg, innerExn) = inherit FarkleException(msg, innerExn)

/// An object that represents a precompiled grammar inside an assembly.
type PrecompiledGrammar private(asm, grammarName, resourceName) =
    static let precompiledGrammarResourceSuffix = ".precompiled.egtn"
    static let assemblyCache = ConditionalWeakTable()
    static let getAllPrecompiledGrammars = ConditionalWeakTable.CreateValueCallback<Assembly,_>(fun asm ->
        asm.GetManifestResourceNames()
        |> Seq.filter (fun name -> name.EndsWith(precompiledGrammarResourceSuffix, StringComparison.Ordinal))
        |> Seq.map (fun name ->
            let grammarName = name.Substring(0, name.Length - precompiledGrammarResourceSuffix.Length)
            grammarName, PrecompiledGrammar(asm, grammarName, name))
        |> readOnlyDict
    )

    static let newPrecompilerLoaderException innerExn =
        PrecompilerLoaderException("Failed to load a precompiled grammar. Try rebuilding \
the assembly with the latest version of Farkle and Farkle.Tools.MSBuild. If the problem persists, \
please open an issue on GitHub", innerExn)

    let grammarThunk = lazy(
        // The stream will definitely be not null.
        use stream = asm.GetManifestResourceStream resourceName
        try
            EGT.ofStreamEx GrammarSource.Precompiled stream
        with
        | e -> raise(newPrecompilerLoaderException e)
    )

    /// The `Assembly` that contains this precompiled grammar.
    member _.Assembly = asm
    /// The precompiled grammar's name.
    member _.GrammarName = grammarName
    /// Gets the actual `Grammar` of this object.
    member _.GetGrammar() = grammarThunk.Value
    static member internal GetResourceName (grammar: Grammar) =
        grammar.Properties.Name + precompiledGrammarResourceSuffix
    /// Gets all precompiled grammars of an assembly.
    /// This function is safe to call from multiple threads and efficient to
    /// call many times with the same assembly. The cache it internally uses
    /// does not prevent the assembly from getting unloaded.
    static member GetAllFromAssembly asm =
        assemblyCache.GetValue(asm, getAllPrecompiledGrammars)

/// A kind of designtime Farkle whose grammar can be precompiled ahead of time.
type PrecompilableDesigntimeFarkle internal(df: DesigntimeFarkle, assembly: Assembly) =
    let df =
        match df with
        | :? PrecompilableDesigntimeFarkle as pcdf -> pcdf.InnerDesigntimeFarkle
        | _ -> df
    // We have to be 100% sure that the designtime Farkle's
    // name never changes because it is part of its identity.
    // All its implementations return a fixed value, but that's
    // an informal rule.
    let name = df.Name
    let metadata = df.Metadata
    let grammarDef = DesigntimeFarkleBuild.createGrammarDefinition df
    /// <inheritDoc cref="DesigntimeFarkle.Name"/>
    member _.Name = name
    /// The `GrammarDefinition` of this designtime Farkle.
    member _.GrammarDefinition = grammarDef
    /// The assembly in which the designtime Farkle was declared.
    member _.Assembly = assembly
    member private _.InnerDesigntimeFarkle = df
    /// Tries to get the `PrecompiledGrammar` object, if it exists in the assembly.
    member _.TryGetPrecompiledGrammar() : [<Nullable(2uy, 1uy)>] _ =
        match PrecompiledGrammar.GetAllFromAssembly(assembly).TryGetValue(name) with
        | true, pg -> Some pg
        | false, _ -> None
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = metadata
    interface DesigntimeFarkleWrapper with
        member _.InnerDesigntimeFarkle = df

/// The typed edition of `PrecompilableDesigntimeFarkle`.
[<Sealed>]
type PrecompilableDesigntimeFarkle<[<Nullable(2uy)>] 'T> internal(df: DesigntimeFarkle<'T>, assembly) =
    inherit PrecompilableDesigntimeFarkle(
        (match df with :? PrecompilableDesigntimeFarkle<'T> as pcdf -> pcdf.InnerDesigntimeFarkle | _ -> df),
        assembly)
    member private _.InnerDesigntimeFarkle = df
    interface DesigntimeFarkle<'T>

/// Functions to create precompilable designtime
/// Farkles and to load precompiled grammars from them.
/// This module is the bridge between the RuntimeFarkle and precompiler APIs.
module internal PrecompilerInterface =

    /// Tries to find a precompiled grammar for the given
    /// designtime Farkle, and returns it if found.
    let internal getGrammarOrBuild (df: DesigntimeFarkle) =
        match df with
        | :? PrecompilableDesigntimeFarkle as pcdf ->
            let grammar =
                match pcdf.TryGetPrecompiledGrammar() with
                | Some pg -> Ok <| pg.GetGrammar()
                | None -> DFB.buildGrammarOnly pcdf.GrammarDefinition
            grammar
        | df -> df |> DFB.createGrammarDefinition |> DFB.buildGrammarOnly
