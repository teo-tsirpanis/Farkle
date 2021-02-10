// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Builder
open Farkle.Grammar
open Farkle.Tools.Precompiler
open Farkle.Tools.Templating
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Mono.Cecil
open Sigourney
open System
open System.IO

/// An MSBuild task that precompiles the grammars of an assembly.
type FarklePrecompileTask() =
    inherit MSBuildWeaver()
    let mutable precompiledGrammars = []
    let mutable generatedHtmlFiles = []

    member val GenerateHtml = false with get, set
    member val HtmlOutputPath = null with get, set
    [<Output>]
    member val GeneratedHtmlFiles = Array.Empty<_>() with get, set

    member private this.DoGenerateHtml grammar =
        let grammarInput = {Grammar = grammar; GrammarPath = this.AssemblyPath}
        let htmlOptions = {
            CustomHeadContent = ""
            NoCss = false
            NoLALRStates = false
            NoDFAStates = false
        }
        let templateType = GrammarHtml(grammarInput, htmlOptions)
        if String.IsNullOrWhiteSpace this.HtmlOutputPath then
            this.Log2.Error("The HtmlOutputPath task parameter is not assigned.")
        else
            match TemplateEngine.renderTemplate this.Log2 templateType with
            | Ok output ->
                let grammarName = grammar.Properties.Name
                let htmlPath =
                    Path.Combine(this.HtmlOutputPath, Path.ChangeExtension(grammarName, output.FileExtension))
                    |> Path.GetFullPath
                this.Log2.Information("Writing documentation of {GrammarName} at {HtmlPath}...", grammarName, htmlPath)
                File.WriteAllText(htmlPath, output.Content)

                generatedHtmlFiles <- TaskItem htmlPath :> ITaskItem :: generatedHtmlFiles
            | Error() ->
                this.Log2.Error("There was an error with the HTML generator. Please report it on GitHub.")

    override this.Execute() =
        let grammars = precompileAssemblyFromPath this.Log2 this.AssemblyReferences this.AssemblyPath
        match grammars with
        | Ok grammars ->
            precompiledGrammars <-
                grammars
                |> List.choose (fun x ->
                    match x with
                    | Successful grammar ->
                        if this.GenerateHtml then
                            this.DoGenerateHtml grammar
                        Some grammar
                    | PrecompilingFailed(name, [error]) ->
                        this.Log2.Error("Error while precompiling {GrammarName}: {ErrorMessage}", name, error)
                        None
                    | PrecompilingFailed(name, errors) ->
                        this.Log2.Error("Errors while precompiling {GrammarName}.", name)
                        for error in errors do
                            this.Log2.Error("{BuildError}", error)
                        None
                    | DiscoveringFailed(typeName, fieldName, e) ->
                        this.Log2.Error("Exception thrown while getting the value of field {FieldName} in type {TypeName}.", fieldName, typeName)
                        this.Log2.Error("{Exception}", e)
                        None)

            this.GeneratedHtmlFiles <- Array.ofList generatedHtmlFiles

            not this.Log.HasLoggedErrors
            // With our preparation completed, Sigourney will eventually call DoWeave.
            && base.Execute()
        // There are some errors (such as duplicate grammar name errors)
        // that are errors no matter what the user said.
        | Error () -> false
    override _.DoWeave asm =
        use stream = new MemoryStream()
        for grammar in precompiledGrammars do
            EGT.toStreamNeo stream grammar

            // We will try to read the EGTneo file we just
            // generated as a form of self-verification.
            stream.Position <- 0L
            EGT.ofStream stream |> ignore

            let name = PrecompiledGrammar.GetResourceName grammar
            let res = EmbeddedResource(name, ManifestResourceAttributes.Public, stream.ToArray())
            asm.MainModule.Resources.Add res
            stream.SetLength 0L
        not precompiledGrammars.IsEmpty
