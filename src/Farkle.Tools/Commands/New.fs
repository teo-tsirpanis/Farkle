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
open System.IO

type Arguments =
    | [<MainCommand; ExactlyOnce; Last>] GrammarFile of string
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique>] Type of TemplateType
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<EqualsAssignment; AltCommandLine("-prop")>] Property of key: string * value: string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "The *.egt grammar file to parse. Required."
            | Language _ -> "Specifies the language of the template to create. Defaults to F#."
            | Type _ -> "Specifies the type of the template to create, i.e. either a file containing \
the grammar and its symbol types, or a skeleton for a post-processor. Defaults to the former."
            | TemplateFile _ -> "Specifies the template file to use, in case you want a custom one."
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

let toCustomFile fileName =
    fileName
    |> assertFileExists
    |> Result.map CustomFile

let run (args: ParseResults<_>) = either {
    let! grammarFile = args.GetResult GrammarFile |> assertFileExists
    let typ = args.GetResult(Type, defaultValue = TemplateType.Grammar)
    let language = args.GetResult(Language, defaultValue = Language.``F#``)
    let properties = args.GetResults Property
    let! templateSource =
        args.TryPostProcessResult(TemplateFile, toCustomFile)
        |> Option.defaultValue (Ok <| BuiltinTemplate(language, typ))

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
