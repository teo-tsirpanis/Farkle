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

/// This exception gets thrown when Farkle fails to load a precompiled grammar.
/// Such exceptions indicate bugs and should they ever occur, opening an issue
/// on GitHub would be very helpful.
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

/// <summary>An object holding a designtime Farkle whose
/// grammar can be precompiled ahead of time.</summary>
/// <remarks><para>Precompilable designtime Farkles are
/// created by the <c>RuntimeFarkle.markForPrecompile</c>
/// function, the <c>MarkForPrecompile</c> extension method,
/// or their untyped variations. In F# they are built using
/// the <c>RuntimeFarkle.buildPrecompiled</c> function or
/// its untyped variation.</para>
/// <para>Despite their name, they lack the most fundamental
/// property of designtime Farkles: composability. A designtime
/// Farkle is meant to be marked for precompilation once, at the
/// end of the grammar building process.</para></remarks>
/// <seealso cref="PrecompilableDesigntimeFarkle{T}"/>
type PrecompilableDesigntimeFarkle internal(df: DesigntimeFarkle, assembly: Assembly) =
    let name = df.Name
    let grammarDef = DesigntimeFarkleBuild.createGrammarDefinition df
    /// The name of the designtime Farkle held by this object.
    member _.Name = name
    /// The `GrammarDefinition` of this designtime Farkle.
    member _.GrammarDefinition = grammarDef
    /// The assembly from which this object was created.
    /// It must match the assembly that is being compiled.
    member _.Assembly = assembly
    /// The designtime Farkle held by this object.
    member _.InnerDesigntimeFarkle = df
    /// Tries to get the `PrecompiledGrammar` object, if it exists in the assembly.
    member _.TryGetPrecompiledGrammar() : [<Nullable(2uy, 1uy)>] _ =
        match PrecompiledGrammar.GetAllFromAssembly(assembly).TryGetValue(name) with
        | true, pg -> Some pg
        | false, _ -> None

/// <summary>The typed edition of <see cref="PrecompilableDesigntimeFarkle"/>.</summary>
/// <seealso cref="PrecompilableDesigntimeFarkle"/>
[<Sealed>]
type PrecompilableDesigntimeFarkle<[<Nullable(2uy)>] 'T> internal(df: DesigntimeFarkle<'T>, assembly) =
    inherit PrecompilableDesigntimeFarkle(df, assembly)
    /// The typed designtime Farkle held by this object.
    member _.InnerDesigntimeFarkle = df

module internal PrecompilerInterface =

    let buildPrecompiled (df: PrecompilableDesigntimeFarkle) =
        match df.TryGetPrecompiledGrammar() with
        | Some pg -> Ok <| pg.GetGrammar()
        | None -> Error [BuildError.GrammarNotPrecompiled]
