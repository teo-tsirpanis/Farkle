// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.New

open Argu
open Farkle.Monads.Either
open Farkle.Tools
open Farkle.Tools.Templating
open Serilog
open System
open System.IO
open System.Text.Json

type Arguments =
    | [<Unique; MainCommand>] GrammarFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<Unique>] Website
    | [<Unique>] GrammarSkeleton
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique; AltCommandLine("-ns")>] Namespace of string
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-prop")>] Property of string * string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "A composite path of the grammar to process. \
Run 'farkle --explain-composite-paths' to learn their syntax."
            | OutputFile _ -> "Specifies where the output file will be stored. \
Defaults to the grammar's name and extension, with a suffix set by the template, which defaults to '.out.txt'."
            | Website -> "Specifies that an HTML web page describing the grammar should be generated. This is the default."
            | GrammarSkeleton -> "Specifies that a skeleton source file for the grammar should be generated. \
The source's namespace and language can be adjusted by the respective arguments."
            | Language _ -> "Specifies the skeleton source file's language. If not specified, Farkle will \
infer it based on the project files in the current directory; otherwise it will use F#."
            | Namespace _ -> "Specifies the skeleton source file's namespace. \
If not specified, the input file's name will be used."
            | TemplateFile _ -> "Specifies a custom Scriban template file to use. It's documented in Farkle's site."
            | Property _ -> "Specifies an additional property to be passed to your custom template \
via the 'properties.myproperty' Scriban variable."

let tryInferLanguage() =
    let hasExtension =
        let files = Directory.GetFiles(Environment.CurrentDirectory)
        fun ext -> files |> Array.exists (fun path -> Path.GetExtension(path) = ext)
    match hasExtension ".csproj", hasExtension ".fsproj" with
    | true, true ->
        Log.Error("Cannot infer the language to use; there are both C# and F# projects in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()
    | true, false ->
        Log.Debug("No language was specified; inferred to be C#, as there are C# projects in {CurrentFirectory}", Environment.CurrentDirectory)
        Ok Language.``C#``
    | false, true ->
        Log.Debug("No language was specified; inferred to be F#, as there are F# projects in {CurrentFirectory}", Environment.CurrentDirectory)
        Ok Language.``F#``
    | false, false ->
        Log.Debug("Neither a language was specified, nor are there any supported projcets in {CurrentDirectory}. Language is inferred to be F#", Environment.CurrentDirectory)
        Ok Language.``F#``

let getTemplateType grammarInput (args: ParseResults<_>) = either {
    match args.Contains Website, args.Contains GrammarSkeleton, args.TryGetResult TemplateFile with
    | _, false, None ->
        return GrammarWebsite(grammarInput, ())
    | false, true, None ->
        let! language =
            args.TryPostProcessResult(Language, Ok)
            |> Option.defaultWith tryInferLanguage
        let ns = args.TryGetResult Namespace
        return TemplateType.GrammarSkeleton(grammarInput, language, ns)
    | false, false, Some customTemplatePath ->
        let! templatePath = assertFileExists customTemplatePath
        return GrammarCustomTemplate(grammarInput, templatePath)
    | _, true, Some _ | true, _, Some _ | true, true, _ ->
        Log.Error("The '--website', '--grammarskeleton' and '-t' arguments cannot be used at the same time")
        return! Error()
}

let getTemplateOptions (args: ParseResults<_>) =
    let properties = args.GetResults Property
    {CustomProperties = properties}

let run json (args: ParseResults<_>) = either {
    let! grammarInput =
        args.TryGetResult GrammarFile
        |> CompositePath.create
        |> CompositePath.resolve Environment.CurrentDirectory
    let! templateType = getTemplateType grammarInput args
    let templateOptions = getTemplateOptions args

    let! generatedTemplate = TemplateEngine.renderTemplate Log.Logger templateType templateOptions

    let outputFile =
        match args.TryGetResult OutputFile with
        | Some x -> x
        | None ->
            Path.ChangeExtension(grammarInput.GrammarPath, generatedTemplate.FileExtension)
        |> Path.GetFullPath

    if json then
        {|outputFile = outputFile; content = generatedTemplate.Content|}
        |> JsonSerializer.Serialize
        |> printfn "%s"
    else
        Log.Verbose("Creating file at {outputFile}", outputFile)
        File.WriteAllText(outputFile, generatedTemplate.Content)

        Log.Information("Template was created at {outputFile}", outputFile)
}
