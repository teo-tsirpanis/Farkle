// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Grammar
open Farkle.Tools.Precompiler
open Farkle.Tools.Templating
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Sigourney
open System
open System.IO
open System.Threading

/// An MSBuild task that precompiles the grammars of an assembly.
type FarklePrecompileTask() =
    inherit MSBuildWeaver()
    let mutable precompiledGrammars = []
    let mutable generatedHtmlFiles = []

    let cts = new CancellationTokenSource()

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
        try
            let grammars = precompileAssemblyFromPath cts.Token this.Log2 this.AssemblyReferences this.AssemblyPath
            match grammars with
            | Ok grammars ->
                precompiledGrammars <- grammars
                if this.GenerateHtml then
                    for x in grammars do
                        this.DoGenerateHtml x

                this.GeneratedHtmlFiles <- Array.ofList generatedHtmlFiles

                not cts.IsCancellationRequested
                && not this.Log.HasLoggedErrors
                // With our preparation completed, Sigourney will eventually call DoWeave.
                && base.Execute()
            // There are some errors (such as duplicate grammar name errors)
            // that are errors no matter what the user said.
            | Error () -> false
        with
        | :? OperationCanceledException as oce when oce.CancellationToken = cts.Token -> false
    override _.DoWeave asm = weaveGrammars asm precompiledGrammars
    interface ICancelableTask with
        member _.Cancel() = cts.Cancel()
