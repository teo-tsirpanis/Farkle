// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Tools.Precompiler
open Microsoft.Build.Framework
open Sigourney

/// An MSBuild task that precompiles the grammars of an assembly.
type FarklePrecompileTask() =
    inherit MSBuildWeaver()
    let mutable precompiledGrammars = []
    /// Whether to treat grammar precompilation
    /// errors (like LALR conflicts) as warnings.
    member val SuppressGrammarErrors = false with get, set

    member private this.LogSuppressibleError(messageTemplate, x1) =
        if this.SuppressGrammarErrors then
            this.Log2.Warning<'T0>(messageTemplate, x1)
        else
            this.Log2.Error(messageTemplate, x1)
    member private this.LogSuppressibleError(messageTemplate, x1, x2) =
        if this.SuppressGrammarErrors then
            this.Log2.Warning<'T0,'T1>(messageTemplate, x1, x2)
        else
            this.Log2.Error(messageTemplate, x1, x2)
    member private this.LogSuppressibleError(exn, messageTemplate, x) =
        if this.SuppressGrammarErrors then
            this.Log2.Warning<'T0>(exn, messageTemplate, x)
        else
            this.Log2.Error(exn, messageTemplate, x)

    override this.Execute() =
        let grammars = discoverAndPrecompile this.Log2 this.AssemblyReferences this.AssemblyPath
        let mutable gotGrammarError = false
        match grammars with
        | Ok grammars ->
            precompiledGrammars <-
                grammars
                |> List.choose (fun x ->
                    match x with
                    | Successful (name, grammar) ->
                        Some (name, grammar)
                    | PrecompilingFailed(name, [error]) ->
                        this.LogSuppressibleError("Error while precompiling {GrammarName}: {ErrorMessage}", name, error)
                        gotGrammarError <- true
                        None
                    | PrecompilingFailed(name, errors) ->
                        this.LogSuppressibleError("Errors while precompiling {GrammarName}.", name)
                        for error in errors do
                            this.LogSuppressibleError("{BuildError}", error)
                        gotGrammarError <- true
                        None
                    | DiscoveringFailed(fieldName, e) ->
                        this.LogSuppressibleError(e, "Exception thrown while getting the value of field {FieldName}.", fieldName)
                        None)

            if gotGrammarError then
                this.Log.LogMessage(MessageImportance.High, "Hint: you can treat grammar precompilation errors as warnings \
by setting the FarkleSuppressGrammarErrors MSBuild property to true.")

            not this.Log.HasLoggedErrors
            // With our preparation completed, Sigourney will eventually call DoWeave.
            && base.Execute()
        // There are some errors (such as duplicate grammar errors)
        // that are errors no matter what the user said.
        | Error () -> false
    override _.DoWeave asm = weaveAssembly precompiledGrammars asm
