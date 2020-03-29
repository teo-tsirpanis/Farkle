// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.Precompiler

open Farkle.Grammar
open System.Reflection
module DFB = DesigntimeFarkleBuild

let precompiledGrammarNamespace = "__Farkle.PrecompiledGrammar"

let getPrecompiledGrammarResourceName (df: DesigntimeFarkle) =
    sprintf "%s.%s" precompiledGrammarNamespace df.Name

/// A kind of designtime Farkle whose grammar can be precompiled
/// when the assembly containing it is compiled. Its distinguishing
/// feature is that its grammar definition is static and determined
/// the moment an object of this type is created.
type PrecompilableDesigntimeFarkle =
    inherit DesigntimeFarkleWrapper
    /// The grammar definition of the designtime Farkle.
    /// The builder should use this object, instead of
    /// calling DesigntimeFarkleBuild.createGrammarDefinition.
    abstract Grammar: GrammarDefinition
    /// The assembly in which the designtime Farkle was declared.
    /// It is needed so that Farkle knows where to look for the
    /// precompiled grammar (if it exists).
    abstract DeclaringAssembly: Assembly

type PrecompilableDesigntimeFarkle<'T> (df: DesigntimeFarkle<'T>, asm) =
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
        member _.InnerDesigntimeFarkle = upcast df
    interface PrecompilableDesigntimeFarkle with
        member _.Grammar = grammarDef
        member _.DeclaringAssembly = asm

/// Tries to find a precompiled grammar for the given
/// designtime Farkle, and returns it if it finds it
/// and the designtime Farkle is marked,
/// or else it builds it on the spot.
let buildGrammar (df: DesigntimeFarkle) =
    match df with
    | :? PrecompilableDesigntimeFarkle as pcdf ->
        // The grammar is loaded from an EGTneo file in the
        // assembly's resources, if it exists. Errors reading
        // the grammar are unexpected and will throw.
        let grammar =
            pcdf
            |> getPrecompiledGrammarResourceName
            |> pcdf.DeclaringAssembly.GetManifestResourceStream
            |> Option.ofObj
            |> Option.map (EGT.ofStream >> Ok)
            |> Option.defaultWith (fun () -> DFB.buildGrammarOnly pcdf.Grammar)
        grammar
    | df -> df |> DFB.createGrammarDefinition |> DFB.buildGrammarOnly
