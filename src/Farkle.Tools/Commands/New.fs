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
open System.Runtime.InteropServices
open System.Text.Json

type Arguments =
    | [<Unique; MainCommand>] GrammarFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-f")>] Framework of string
    | [<Unique>] Html
    | [<Unique>] ``Custom-head`` of string
    | [<Unique>] ``No-css``
    | [<Unique>] ``No-lalr``
    | [<Unique>] ``No-dfa``
    | [<Unique>] GrammarSkeleton
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique; AltCommandLine("-ns")>] Namespace of string
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<AltCommandLine("-prop")>] Property of string * string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "A composite path of the file to process. It can be a grammar, a .NET assembly or \
a .NET SDK project. Run 'farkle --explain-composite-paths' to learn their syntax."
            | OutputFile _ -> "The path the output file will be stored. \
Defaults to the input file's name, with an extension set by the template, which defaults to '.out.txt'."
            | Configuration _ -> "The configuration the project will be evaluated with. The default for most projects is Debug."
            | Framework _ -> "The target framework of the project. Useful if it uses multi-targeting."
            | Html -> "Generate an HTML web page describing the grammar. This is the default."
            | ``Custom-head`` _ -> "A file whose content will be appended to the resulting HTML page's head."
            | ``No-css`` -> "Do not generate inline CSS for the resulting HTML page."
            | ``No-lalr`` -> "Do not generate the LALR state tables in the resulting HTML page."
            | ``No-dfa`` -> "Do not generate the DFA state tables in the resulting HTML page."
            | GrammarSkeleton -> "Generate a skeleton source file for the grammar in either C# or F#. \
The source's namespace and language can be adjusted by the respective arguments."
            | Language _ -> "The skeleton source file's language. If not specified, Farkle will \
infer it based on the project files in the current directory; otherwise it will use F#."
            | Namespace _ -> "The skeleton source file's namespace. \
If not specified, the input file's name will be used."
            | TemplateFile _ -> "Generate a file using this custom Scriban template. See more in https://teo-tsirpanis.github.io/Farkle/templating.html."
            | Property _ -> "Additional properties to be passed to your custom template \
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
        Log.Debug("No language was specified; inferred to be C#, as there are C# projects in {CurrentDirectory}", Environment.CurrentDirectory)
        Ok Language.``C#``
    | false, true ->
        Log.Debug("No language was specified; inferred to be F#, as there are F# projects in {CurrentDirectory}", Environment.CurrentDirectory)
        Ok Language.``F#``
    | false, false ->
        Log.Debug("Neither a language was specified, nor are there any supported projcets in {CurrentDirectory}. Language is inferred to be F#", Environment.CurrentDirectory)
        Ok Language.``F#``

let getTemplateType grammarInput (args: ParseResults<_>) = either {
    match args.Contains Html, args.Contains GrammarSkeleton, args.TryGetResult TemplateFile with
    | _, false, None ->
        let! customHead = either {
            match args.TryGetResult ``Custom-head`` with
            | Some headFile ->
                let! headFile = assertFileExists headFile
                return File.ReadAllText headFile
            | None -> return ""
        }
        let options = {
            CustomHeadContent = customHead
            NoCss = args.Contains ``No-css``
            NoLALRStates = args.Contains ``No-lalr``
            NoDFAStates = args.Contains ``No-dfa``
        }
        return GrammarHtml(grammarInput, options)
    | false, true, None ->
        let! language =
            args.TryPostProcessResult(Language, Ok)
            |> Option.defaultWith tryInferLanguage
        let ns = args.TryGetResult Namespace
        return TemplateType.GrammarSkeleton(grammarInput, language, ns)
    | false, false, Some customTemplatePath ->
        let! templatePath = assertFileExists customTemplatePath
        let additionalProperties = args.GetResults Property
        let options = {AdditionalProperties = additionalProperties}
        return GrammarCustomTemplate(grammarInput, templatePath, options)
    | _, true, Some _ | true, _, Some _ | true, true, _ ->
        Log.Error("The {Html}, {GrammarSkeleton} and {T} arguments cannot be used at the same time",
            "--html", "--grammarskeleton", "-t")
        return! Error()
}

let warnOnUnusedArguments (grammarPath: string) (args: ParseResults<_>) =
    let doWarnIfNot isUsed (arg: Quotations.Expr<_ -> _>) (argName: string) =
        if not isUsed && args.Contains arg then
            Log.Warning("Argument {IgnoredArgument} is ignored.", argName)
    let doWarnIfNotOpt isUsed (arg: Quotations.Expr<_>) (argName: string) =
        if not isUsed && args.Contains arg then
            Log.Warning("Argument {IgnoredArgument} is ignored.", argName)
    let isSkeleton = args.Contains GrammarSkeleton
    let isCustomTemplate = args.Contains TemplateFile
    let isHtml = args.Contains Html || (isSkeleton = isCustomTemplate)
    let isProjectFile =
        let extension = Path.GetExtension(grammarPath.AsSpan())
        isProjectExtension extension
    doWarnIfNot isHtml <@ ``Custom-head`` @> "--custom-head"
    doWarnIfNotOpt isHtml <@ ``No-css`` @> "--no-css"
    doWarnIfNotOpt isHtml <@ ``No-lalr`` @> "--no-lalr"
    doWarnIfNotOpt isHtml <@ ``No-dfa`` @> "--no-dfa"
    doWarnIfNot isSkeleton <@ Language @> "-lang"
    doWarnIfNot isSkeleton <@ Namespace @> "-ns"
    doWarnIfNot isCustomTemplate <@ Property @> "-prop"
    doWarnIfNot isProjectFile <@ Configuration @> "-c"

let run json (args: ParseResults<_>) = either {
    let projectOptions = {
        ProjectResolver.Configuration = args.TryGetResult Configuration
        ProjectResolver.TargetFramework = args.TryGetResult Framework
    }
    let! grammarInput =
        args.TryGetResult GrammarFile
        |> CompositePath.create
        |> CompositePath.resolve projectOptions Environment.CurrentDirectory
    let! templateType = getTemplateType grammarInput args
    warnOnUnusedArguments grammarInput.GrammarPath args

    let! generatedTemplate = TemplateEngine.renderTemplate Log.Logger templateType

    let outputFile =
        match args.TryGetResult OutputFile with
        | Some x -> Path.GetFullPath x
        | None ->
            // CompositePath.resolve ensures that this is an absolute path.
            let grammarPath = grammarInput.GrammarPath.AsSpan()
            let directory = Path.GetDirectoryName grammarPath
            let mutable separatorChar = Path.DirectorySeparatorChar
            let separator = MemoryMarshal.CreateReadOnlySpan(&separatorChar, 1)
            let fileName =
                if isGrammarExtension (Path.GetExtension grammarPath) then
                    Path.GetFileNameWithoutExtension grammarPath
                else
                    (sanitizeUnsafeFileName Log.Logger grammarInput.Grammar.Properties.Name).AsSpan()
            let extension = generatedTemplate.FileExtension.AsSpan()
            String.Concat(directory, separator, fileName, extension)

    if json then
        {|outputFile = outputFile; content = generatedTemplate.Content|}
        |> JsonSerializer.Serialize
        |> printfn "%s"
    else
        Log.Verbose("Creating file at {OutputFile}", outputFile)
        File.WriteAllText(outputFile, generatedTemplate.Content)

        Log.Information("Template was created at {OutputFile}", outputFile)
}
