// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Monads.Either
open Farkle.Tools.Templating
open Farkle.Tools.Templating.BuiltinTemplates
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Serilog
open Serilog.Sinks.MSBuild
open System
open System.IO

/// An MSBuild task that generates a skeleton program from a Farkle grammar and a Scriban template.
type FarkleCreateTemplateTask() =
    inherit Task()

    let hasValue x = String.IsNullOrWhiteSpace x |> not

    let assertFileExists (log: ILogger) fileName =
        if File.Exists fileName then
            Ok fileName
        else
            log.Error("File {fileName} does not exist.", fileName)
            Error()

    let (|EqualTo|_|) x1 x2 =
        if StringComparer.OrdinalIgnoreCase.Compare(x1, x2) = 0 then
            Some EqualTo
        else
            None

    [<Required>]
    /// <summary>The path to the grammar file in question.</summary>
    /// <remarks>It is required.</remarks>
    member val Grammar = null with get, set

    /// <summary>The programming language of the generated template.</summary>
    /// <remarks>This property is used to load a built-in template. If both it,
    /// and <see cref="CustomTemplate"/> are set, then the latter get used. But
    /// if none of the two is set, it is an error.</remarks>
    /// <seealso cref="CustomTemplate"/>
    member val Language = null with get, set

    /// <summary>The path of the custom Scriban template to use.</summary>
    /// <remarks>This property takes precedence if both it and
    /// <see cref="Language"/> is used.</remarks>
    /// <seealso cref="Language"/>
    member val CustomTemplate = null with get, set

    /// <summary>An optional custom namespace for the generated file.</summary>
    member val Namespace = null with get, set

    /// <summary>The file path to write the output to.</summary>
    /// <remarks>If not specified, it defaults to the name of the grammar file,
    /// with the extension set by the template, which defaults to <c>.out</c>.
    member val OutputFile = null with get, set

    [<Output>]
    /// <summary>The file path where the output was generated to.</summary>
    member val GeneratedTo = null with get, set

    member private this.DoIt log = either {
        let! grammarPath = assertFileExists log this.Grammar
        let! templateSource =
            match hasValue this.CustomTemplate, hasValue this.Language with
            | true, _ ->
                this.CustomTemplate
                |> assertFileExists log
                |> Result.map (fun x ->
                    log.Debug("Using user-provided template at {CustomTemplatePath}", x)
                    CustomFile x)
            | false, true ->
                match this.Language with
                | EqualTo "F#" -> Ok Language.``F#``
                | EqualTo "C#" -> Ok Language.``C#``
                | _ ->
                    log.Error("Language {Language} is not recognized", this.Language)
                    Error()
                |> Result.map (fun lang ->
                    log.Debug("Using built-in template for language {Language}", lang)
                    BuiltinTemplate(lang, TemplateType.Grammar))
            | false, false ->
                log.Error("Need to specify either a language, or a custom template"); Error()
        let ns = if hasValue this.Namespace then Some this.Namespace else None

        let! generatedTemplate = TemplateEngine.renderTemplate log ns grammarPath templateSource

        this.GeneratedTo <-
            if hasValue this.OutputFile then
                this.OutputFile
            else
                Path.ChangeExtension(grammarPath, generatedTemplate.FileExtension)

        this.Log.LogMessage("{0} -> {1}", Path.GetFileName(this.Grammar), this.GeneratedTo)
        File.WriteAllText(this.GeneratedTo, generatedTemplate.Content)
    }

    override this.Execute() =
        use log =
            LoggerConfiguration()
                .MinimumLevel.Verbose() // MSBuild will take care.
                .WriteTo.MSBuild(this)
                .CreateLogger()
        try
            let didSucceed =
                match this.DoIt(log.ForContext(MSBuildProperties.File, this.Grammar)) with
                | Ok () -> true
                | Error () -> false
            didSucceed && not this.Log.HasLoggedErrors
        with
        | ex -> this.Log.LogErrorFromException(ex, true, true, this.Grammar); false
