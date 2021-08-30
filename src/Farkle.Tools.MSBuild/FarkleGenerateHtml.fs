// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Grammar
open Farkle.Tools
open Farkle.Tools.Templating
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Serilog
open Serilog.Sinks.MSBuild
open System
open System.IO

/// An MSBuild task that generated HTML files from
/// the precompiled grammars of an assembly.
/// Can run in all editions of MSBuild.
type FarkleGenerateHtml() =
    inherit Task()

    [<Required>]
    member val AssemblyPath = "" with get, set

    [<Required>]
    member val OutputDirectory = "" with get, set

    [<Output>]
    member val GeneratedFiles = Array.Empty() with get, set

    member private this.DoGenerateHtml log (generatedHtmlFiles: ITaskItem ResizeArray) grammar =
        let grammarInput = {Grammar = grammar; GrammarPath = this.AssemblyPath}
        let htmlOptions = {
            CustomHeadContent = ""
            NoCss = false
            NoLALRStates = false
            NoDFAStates = false
        }
        let templateType = GrammarHtml(grammarInput, htmlOptions)
        match TemplateEngine.renderTemplate log templateType with
        | Ok output ->
            let grammarName = sanitizeUnsafeFileName log grammar.Properties.Name
            let htmlPath =
                Path.Combine(this.OutputDirectory, Path.ChangeExtension(grammarName, output.FileExtension))
                |> Path.GetFullPath
            log.Information("Writing documentation of {GrammarName:l} at {HtmlPath}...", grammarName, htmlPath)
            File.WriteAllText(htmlPath, output.Content)

            generatedHtmlFiles.Add(TaskItem htmlPath)
        | Error() ->
            log.Error("There was an error with the HTML generator. Please report it on GitHub.")

    override this.Execute() =
        use logRaw =
            LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.MSBuild(this)
                .CreateLogger()
        let log = logRaw.ForContext(MSBuildProperties.File, this.AssemblyPath)

        use loader = new PrecompiledAssemblyFileLoader(this.AssemblyPath)
        let generatedHtmlFiles = ResizeArray()
        for kvp in loader.Grammars do
            this.DoGenerateHtml log generatedHtmlFiles (kvp.Value.GetGrammar())
        this.GeneratedFiles <- generatedHtmlFiles.ToArray()

        not this.Log.HasLoggedErrors
