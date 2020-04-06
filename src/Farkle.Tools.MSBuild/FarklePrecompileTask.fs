// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Tools
open Microsoft.Build.Framework
open Sigourney

/// An MSBuild task that precompiles the grammars of an assembly.
type FarklePrecompileTask() =
    inherit MSBuildWeaver()
    let mutable precompiledGrammars = []
    [<Required>]
    /// The references of the assembly to be precompiled.
    member val References = Array.empty<ITaskItem> with get, set
    /// Whether to treat grammar precompilation
    /// errors (like LALR conflicts) as warnings.
    member val SuppressGrammarErrors = false with get, set
    override this.Execute() =
        let references =
            this.References
            |> Array.map (fun x -> x.ItemSpec)
        let log = this.Log2
        let grammars = Precompiler.discoverAndPrecompile log references this.AssemblyPath
        match grammars with
        | Ok grammars ->
            precompiledGrammars <-
                grammars
                |> List.choose (fun (name, grammar) ->
                    match grammar with
                    | Ok grammar ->
                        Some (name, grammar)
                    | Error msg when this.SuppressGrammarErrors ->
                        // We cannot capture Log2 at this point because it is protected.
                        log.Warning("Error while precompiling {Grammar}: {ErrorMessage}", name, msg)
                        None
                    | Error msg ->
                        log.Error("Error while precompiling {Grammar}: {ErrorMessage}", name, msg)
                        None)
            if this.Log.HasLoggedErrors then
                this.Log.LogMessage(MessageImportance.High, "You can treat grammar precompilation errors as warnings \
by setting the FarkleSuppressGrammarErrors MSBuild property to true.")
            not this.Log.HasLoggedErrors
            // With our preparation completed, Sigourney will eventually call DoWeave.
            && base.Execute()
        // There are some errors (such as duplicate grammar errors)
        // that are errors no matter what the user said.
        | Error () -> false
    override _.DoWeave asm = Precompiler.weaveAssembly precompiledGrammars asm
