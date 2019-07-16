// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.New

open Argu
open Farkle.Monads.Either
open Farkle.Tools.Templating
open Farkle.Tools.Templating.BuiltinTemplates
open Serilog
open System
open System.IO

type Arguments =
    | [<Unique; MainCommand>] GrammarFile of string
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique>] Type of TemplateType
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<EqualsAssignment; AltCommandLine("-prop")>] Property of key: string * value: string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "The EGT grammar file to parse. \
Otherwise, the EGT file in the current directory, if only one exists."
            | Language _ -> "Specifies the language of the template to create. If there is a C# or F# project, \
defaults to this language. If there are both, the language must be specified. If there is neither, defaults to F#."
            | Type _ -> "Specifies the type of the template to create, i.e. either a file containing \
the grammar and its symbol types, or a skeleton for a post-processor. Defaults to the latter."
            | TemplateFile _ -> "Specifies the template file to use, in case you want a custom one. \
In this case, the language is completely ignored."
            | OutputFile _ -> "Specifies where the generated output will be stored. \
Defaults to the grammar's name and extension, with a suffix set by the template, which defaults to 'out'."
            | Property _ -> "Specifies an additional property of the grammar. Keys are case-insensitive. \
These can be retrieved in the templates by grammar.properties[\"Key\"]. \
For example, the property \"Name\" determines the namespace of the grnerated source files."

let assertFileExists fileName =
    if File.Exists fileName then
        Ok fileName
    else
        Log.Error("File {fileName} does not exist.", fileName)
        Error()

let tryInferGrammarFile() =
    Environment.CurrentDirectory
    |> Directory.EnumerateFiles
    |> Seq.filter (Path.GetExtension >> (=) ".egt")
    |> List.ofSeq
    |> function
    | [x] ->
        Log.Debug("No grammar file was specified; using {GrammarFile}", x)
        Ok x
    | [] ->
        Log.Error("Could not find an EGT file in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()
    | _ ->
        Log.Error("More than one EGT files were found in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()

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

let toCustomFile fileName =
    fileName
    |> assertFileExists
    |> Result.map CustomFile

let run (args: ParseResults<_>) = either {
    let! grammarFile =
        args.TryPostProcessResult(GrammarFile, assertFileExists)
        |> Option.defaultWith tryInferGrammarFile
    let typ = args.GetResult(Type, defaultValue = TemplateType.PostProcessor)
    let properties = args.GetResults(Property)
    let! templateSource =
        // First, see if a custom template is specified.
        args.TryPostProcessResult(TemplateFile, toCustomFile)
        // If not,
        |> Option.defaultWith (fun () ->
            // see if a language is specified.
            args.TryPostProcessResult(Language, Ok)
            // If not, try to infer what it is.
            |> Option.defaultWith tryInferLanguage
            // If we have managed to get a language, it's OK.
            |> Result.map (fun lang -> BuiltinTemplate(lang, typ)))

    let! generatedTemplate =
        TemplateEngine.renderTemplate
            Log.Logger
            // The folder has a dash, not an underscore!
            // And the hell the namespace here must have the name
            // of the folder, while it doesn't on Farkle.Tools.MSBuild??
            // TODO: Find out why.
            "Farkle.Tools.builtin_templates"
            properties
            grammarFile
            templateSource

    let outputFile =
        args.TryGetResult OutputFile
        |> Option.defaultWith (fun () -> Path.ChangeExtension(grammarFile, generatedTemplate.FileExtension))
        |> Path.GetFullPath

    Log.Verbose("Creating file at {outputFile}", outputFile)
    File.WriteAllText(outputFile, generatedTemplate.Content)

    Log.Information("Template was created at {outputFile}", outputFile)
}
