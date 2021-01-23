// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Tools.Precompiler
open Farkle.Tools.Templating
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Sigourney
open System
open System.IO

/// An MSBuild task that precompiles the grammars of an assembly.
type FarklePrecompileTask() =
    inherit MSBuildWeaver()
    let mutable precompiledGrammars = []
    let mutable generatedDocumentationFiles = []
    /// Whether to treat grammar precompilation
    /// errors (like LALR conflicts) as warnings.
    member val SuppressGrammarErrors = false with get, set

    member val GenerateDocumentation = false with get, set
    member val DocumentationOutputPath = null with get, set
    [<Output>]
    member val GeneratedDocumentationFiles = Array.Empty<_>() with get, set

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

    member private this.DoGenerateDocumentation grammar =
        let grammarInput = {Grammar = grammar; GrammarPath = this.AssemblyPath}
        let htmlOptions = {
            CustomHeadContent = ""
            NoCss = false
            NoLALRStates = false
            NoDFAStates = false
        }
        let templateType = GrammarHtml(grammarInput, htmlOptions)
        if String.IsNullOrWhiteSpace this.DocumentationOutputPath then
            this.Log2.Error("The DocumentationOutputPath task parameter is not assigned.")
        else
            match TemplateEngine.renderTemplate this.Log2 templateType with
            | Ok output ->
                let grammarName = grammar.Properties.Name
                let htmlPath =
                    Path.Combine(this.DocumentationOutputPath, Path.ChangeExtension(grammarName, output.FileExtension))
                    |> Path.GetFullPath
                this.Log2.Information("Writing documentation of {GrammarName} at {DocumentationPath}...", grammarName, htmlPath)
                File.WriteAllText(htmlPath, output.Content)

                generatedDocumentationFiles <- TaskItem htmlPath :> ITaskItem :: generatedDocumentationFiles
            | Error() ->
                this.Log2.Error("There was an error with the documentation generator. Please report it on GitHub.")

    override this.Execute() =
        let grammars = discoverAndPrecompile this.Log2 this.AssemblyReferences this.AssemblyPath
        let mutable gotGrammarError = false
        match grammars with
        | Ok grammars ->
            precompiledGrammars <-
                grammars
                |> List.choose (fun x ->
                    match x with
                    | Successful grammar ->
                        if this.GenerateDocumentation then
                            this.DoGenerateDocumentation grammar
                        Some grammar
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
                    | DiscoveringFailed(typeName, fieldName, e) ->
                        this.Log2.Error("Exception thrown while getting the value of field {FieldName} in type {TypeName}.", fieldName, typeName)
                        this.Log2.Error("{Exception}", e)
                        None)

            this.GeneratedDocumentationFiles <- Array.ofList generatedDocumentationFiles

            if gotGrammarError && not this.SuppressGrammarErrors then
                this.Log.LogMessage(MessageImportance.High, "Hint: you can treat grammar precompilation errors \
as warnings by setting the FarkleSuppressGrammarErrors MSBuild property to true.")

            not this.Log.HasLoggedErrors
            // With our preparation completed, Sigourney will eventually call DoWeave.
            && base.Execute()
        // There are some errors (such as duplicate grammar name errors)
        // that are errors no matter what the user said.
        | Error () -> false
    override _.DoWeave asm = weaveAssembly precompiledGrammars asm
