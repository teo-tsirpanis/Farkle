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
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-f")>] Framework of string
    | [<Unique>] Html
    | [<Unique>] ``Custom-head`` of string
    | [<Unique>] ``No-css``
    | [<Unique>] ``No-lalr``
    | [<Unique>] ``No-dfa``
    | [<Unique; Hidden>] GrammarSkeleton
    | [<Unique; Hidden; AltCommandLine("-lang")>] Language of string
    | [<Unique; Hidden; AltCommandLine("-ns")>] Namespace of string
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
            | TemplateFile _ -> "Generate a file using this custom Scriban template. See more in https://farkle.dev/templating.html."
            | Property _ -> "Additional properties to be passed to your custom template \
via the 'properties.myproperty' Scriban variable."

let getTemplateType grammarInput (args: ParseResults<_>) = either {
    match args.Contains Html, args.Contains GrammarSkeleton, args.TryGetResult TemplateFile with
    | _, false, None ->
        let customHead =
            match args.TryGetResult ``Custom-head`` with
            | Some headFile -> File.ReadAllText headFile
            | None -> ""
        let options = {
            CustomHeadContent = customHead
            NoCss = args.Contains ``No-css``
            NoLALRStates = args.Contains ``No-lalr``
            NoDFAStates = args.Contains ``No-dfa``
        }
        return GrammarHtml(grammarInput, options)
    | _, true, _ ->
        Log.Error("The --grammar-skeleton argument is not supported starting from Farkle 7.")
        return! Error()
    | false, false, Some customTemplatePath ->
        let additionalProperties = args.GetResults Property
        let options = {AdditionalProperties = additionalProperties}
        return GrammarCustomTemplate(grammarInput, customTemplatePath, options)
    | true, _, Some _ ->
        Log.Error("The {Html:l} and {T:l} arguments cannot be used at the same time.",
            "--html", "-t")
        return! Error()
}

let warnOnUnusedArguments (grammarPath: string) (args: ParseResults<_>) =
    let doWarnIfNot isUsed (arg: Quotations.Expr<_ -> _>) (argName: string) =
        if not isUsed && args.Contains arg then
            Log.Warning("Argument {IgnoredArgument} is ignored.", argName)
    let doWarnIfNotOpt isUsed (arg: Quotations.Expr<_>) (argName: string) =
        if not isUsed && args.Contains arg then
            Log.Warning("Argument {IgnoredArgument} is ignored.", argName)
    let doWarnIgnored (arg: Quotations.Expr<_ -> _>) (argName: string) =
        if args.Contains arg then
            Log.Warning("Argument {IgnoredArgument} is no longer supported and ignored.", argName)
    let isCustomTemplate = args.Contains TemplateFile
    let isHtml = args.Contains Html || not isCustomTemplate
    let isProjectFile =
        let extension = Path.GetExtension(grammarPath.AsSpan())
        isProjectExtension extension
    doWarnIfNot isHtml <@ ``Custom-head`` @> "--custom-head"
    doWarnIfNotOpt isHtml <@ ``No-css`` @> "--no-css"
    doWarnIfNotOpt isHtml <@ ``No-lalr`` @> "--no-lalr"
    doWarnIfNotOpt isHtml <@ ``No-dfa`` @> "--no-dfa"
    doWarnIgnored <@ Language @> "-lang"
    doWarnIgnored <@ Namespace @> "-ns"
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
            let directory = Environment.CurrentDirectory.AsSpan()
            let mutable separatorChar = Path.DirectorySeparatorChar
            let separator = ReadOnlySpan(&separatorChar)
            let fileName =
                let grammarPath = grammarInput.GrammarPath.AsSpan()
                if isGrammarExtension (Path.GetExtension grammarPath) then
                    Path.GetFileNameWithoutExtension grammarPath
                else
                    (sanitizeUnsafeFileName Log.Logger grammarInput.Grammar.GrammarInfo.Name).AsSpan()
            let extension = generatedTemplate.FileExtension.AsSpan()
            String.Concat(directory, separator, fileName, extension)

    if json then
        {|outputFile = outputFile; content = generatedTemplate.Content|}
        |> JsonSerializer.Serialize
        |> printfn "%s"
    else
        Log.Verbose("Creating file at {OutputFile:l}.", outputFile)
        File.WriteAllText(outputFile, generatedTemplate.Content)

        Log.Information("Template was created at {OutputFile:l}.", outputFile)
}
