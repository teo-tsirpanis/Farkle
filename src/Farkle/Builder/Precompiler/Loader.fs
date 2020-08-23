// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.Precompiler

open Farkle.Builder
open Farkle.Grammar
open System
open System.Reflection

module DFB = DesigntimeFarkleBuild

type PrecompilerLoaderException(msg, innerExn: exn) = inherit exn(msg, innerExn)

/// A kind of designtime Farkle whose grammar can be precompiled
/// when the assembly containing it is compiled. Its distinguishing
/// feature is that its grammar definition is static and determined
/// the moment an object of this type is created.
type internal PrecompilableDesigntimeFarkle =
    inherit DesigntimeFarkleWrapper
    /// The grammar definition of the designtime Farkle.
    /// The builder should use this object, instead of
    /// calling DesigntimeFarkleBuild.createGrammarDefinition.
    abstract Grammar: GrammarDefinition
    /// The assembly in which the designtime Farkle was declared.
    /// It is needed so that Farkle knows where to look for the
    /// precompiled grammar (if it exists).
    abstract DeclaringAssembly: Assembly

type private PrecompilableDesigntimeFarkle<'T> (df: DesigntimeFarkle, asm) =
    // We have to be 100% sure that the designtime Farkle's
    // name never changes because it is part of its identity.
    // All its implementations return a fixed value, but that's
    // an informal rule.
    let name = df.Name
    let grammarDef = DesigntimeFarkleBuild.createGrammarDefinition df
    interface DesigntimeFarkle with
        member _.Name = name
        member _.Metadata = df.Metadata
    interface DesigntimeFarkle<'T>
    interface DesigntimeFarkleWrapper with
        member _.InnerDesigntimeFarkle = df
    interface PrecompilableDesigntimeFarkle with
        member _.Grammar = grammarDef
        member _.DeclaringAssembly = asm

/// Functions to load precompiled grammars
/// from assemblies using reflection.
module internal Loader =

    let private suppressLoaderErrorsSwitch = "Switch.Farkle.Builder.Precompiler.SuppressLoaderErrors"

    let private shouldSuppressLoaderErrors<'a> =
        let switchFound, switchEnabled = AppContext.TryGetSwitch suppressLoaderErrorsSwitch
        switchFound && switchEnabled

    let private newPrecompilerLoaderException =
        let msg =
            sprintf "Failed to load a precompiled grammar. Try rebuilding the assembly with the latest version \
of Farkle. To suppress this error and build the grammar, set the '%s' AppContext switch to true."
                suppressLoaderErrorsSwitch
        fun (innerExn: exn) -> PrecompilerLoaderException(msg, innerExn)

    /// Gets the manifest resource name of the precompiled grammar with the given name.
    let getPrecompiledGrammarResourceName name = sprintf "%s.precompiled.egtn" name

    /// Tries to find a precompiled grammar for the given
    /// designtime Farkle, and returns it if found.
    let getGrammarOrBuild (df: DesigntimeFarkle) =
        match df with
        | :? PrecompilableDesigntimeFarkle as pcdf ->
            try
                let grammar =
                    pcdf.Name
                    |> getPrecompiledGrammarResourceName
                    |> pcdf.DeclaringAssembly.GetManifestResourceStream
                    |> function
                    | null -> DFB.buildGrammarOnly pcdf.Grammar
                    | stream -> stream |> EGT.ofStream |> Ok
                grammar
            with
            | _ when shouldSuppressLoaderErrors ->
                DFB.buildGrammarOnly pcdf.Grammar
            | e ->
                raise(newPrecompilerLoaderException e)
        | df -> df |> DFB.createGrammarDefinition |> DFB.buildGrammarOnly

    /// Marks the given designtime Farkle as eligible to
    /// be precompiled. The assembly it resides is also given.
    let prepare (df: DesigntimeFarkle<'T>) asm =
        match df with
        | :? PrecompilableDesigntimeFarkle as pcdf ->
            PrecompilableDesigntimeFarkle(pcdf.InnerDesigntimeFarkle, asm)
        | df -> PrecompilableDesigntimeFarkle(df, asm)
        :> DesigntimeFarkle<'T>
